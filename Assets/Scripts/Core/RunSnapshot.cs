using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

namespace Arena
{
    /// Run snapshots (dev tool, F6 / F7 and the F1 dock's Snapshots sheet): the whole match state at one moment, to load back
    /// later and play on from exactly there. Capture walks every field of the Game (and everything it reaches: players, enemies,
    /// bullets, the map, timers, the random generators) with reflection and writes it to bytes; Restore reads them back and puts
    /// every field in place on the live Game. Nothing has to be listed by hand, so new state is picked up on its own.
    ///   Not kept: the Session and network, the recorders, hooks (delegate fields on the Game) and dev toggles (Bot) — see Skip.
    ///   Shared definitions (objects reachable from static fields, e.g. layouts and squads) are written as a path and come back
    ///   as the same object. The Game itself comes back as the live Game.
    ///   Tolerant of code changes: fields are matched by name, a field that's gone is skipped, a new one keeps its live value.
    ///   A type that's gone (renamed class) fails the load cleanly, before anything is changed.
    /// Loads only on the host with no guests connected (guests would not get the whole state). Files: Profile.Folder/snapshots.
    public static class RunSnapshot
    {
        const string Magic = "ARSNAP";
        public const int Format = 1;
        public const string Ext = ".snap";

        /// What a snapshot file says about itself (read without parsing the state).
        public class Info
        {
            public string Path, Name, Mode, Party;
            public int Wave, Players, Enemies, Bytes;
            public string State;
            public float RunTime;
            public DateTime Created;  // UTC
        }

        // --- Public API ------------------------------------------------------------------------------------------

        /// The whole match as bytes (header + state). Call between ticks.
        public static byte[] Capture(Game g, string name)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms, Encoding.UTF8);
            w.Write(Magic);
            w.Write(Format);
            w.Write(name ?? "");
            w.Write(DateTime.UtcNow.Ticks);
            w.Write(g.ModeRules.Name ?? "");
            w.Write(g.ShownWave);
            w.Write(g.State.ToString());
            w.Write(g.RunTime);
            w.Write(g.Players.Count);
            w.Write(g.Enemies.Count);
            var party = new StringBuilder();
            foreach (var p in g.Players.Values)
                party.Append(party.Length > 0 ? ", " : "").Append(p.Name);
            w.Write(party.ToString());
            new GraphWriter(w, g).WriteRoot();
            w.Flush();
            return ms.ToArray();
        }

        /// Why this game can't load a snapshot now (null: it can).
        public static string CantLoad(Game g, Session s)
        {
            if (s == null || !s.IsHost || !g.Running)
                return "Load a snapshot while hosting a run (start any solo run first).";
            if (s.Net != null && s.Net.PeerCount > 0)
                return "Snapshots load only with no guests connected.";
            return null;
        }

        /// Put the snapshot's state on the live game. Returns null when it worked, or what went wrong (nothing changed then).
        /// `notes`: what was skipped (fields or methods that no longer exist), "" when everything matched.
        public static string Restore(Game g, byte[] data, out string notes)
        {
            notes = "";
            Dictionary<string, object> fields;
            GraphReader reader;
            try
            {
                var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
                ReadHeader(r, null);
                reader = new GraphReader(r, g);
                fields = reader.ReadRoot();
            }
            catch (Exception e)
            {
                return "Can't read the snapshot: " + e.Message;
            }
            int missing = 0;
            foreach (var kv in fields)
            {
                FieldInfo f;
                if (!GameFields().TryGetValue(kv.Key, out f))
                {
                    missing++;
                    continue;
                }
                try
                {
                    f.SetValue(g, Fit(kv.Value, f.FieldType));
                }
                catch (Exception)
                {
                    missing++;
                }
            }
            g.AfterSnapshotLoad();
            if (missing > 0)
                notes += missing + " old field" + (missing == 1 ? "" : "s") + " skipped";
            if (reader.Dropped > 0)
                notes += (notes.Length > 0 ? ", " : "") + reader.Dropped + " value" + (reader.Dropped == 1 ? "" : "s") + " dropped (code changed)";
            return null;
        }

        /// The header of a snapshot file, or null if it isn't one.
        public static Info ReadInfo(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var r = new BinaryReader(fs, Encoding.UTF8);
                    var info = new Info { Path = path, Bytes = (int)fs.Length };
                    ReadHeader(r, info);
                    return info;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void ReadHeader(BinaryReader r, Info info)
        {
            if (r.ReadString() != Magic)
                throw new InvalidDataException("not a snapshot file");
            int format = r.ReadInt32();
            if (format != Format)
                throw new InvalidDataException("snapshot format " + format + ", this build reads " + Format);
            info = info ?? new Info();
            info.Name = r.ReadString();
            info.Created = new DateTime(r.ReadInt64(), DateTimeKind.Utc);
            info.Mode = r.ReadString();
            info.Wave = r.ReadInt32();
            info.State = r.ReadString();
            info.RunTime = r.ReadSingle();
            info.Players = r.ReadInt32();
            info.Enemies = r.ReadInt32();
            info.Party = r.ReadString();
        }

        // --- Files -----------------------------------------------------------------------------------------------

        public const int Kept = 40;  // newest files kept in the folder

        public static string Folder => Profile.Folder == null ? null : System.IO.Path.Combine(Profile.Folder, "snapshots");

        /// Capture and write to a new file in Folder (the oldest beyond Kept are deleted). Returns the path, or null.
        public static string SaveFile(Game g, string name)
        {
            var folder = Folder;
            if (folder == null)
                return null;
            Directory.CreateDirectory(folder);
            var bytes = Capture(g, name);
            string path = System.IO.Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + Ext);
            File.WriteAllBytes(path, bytes);
            var all = List();
            for (int i = Kept; i < all.Count; i++)
                try { File.Delete(all[i].Path); } catch (Exception ex) { Diag.Warn("snapshot: couldn't delete the old snapshot " + all[i].Path, ex); }
            return path;
        }

        /// Every snapshot in Folder, newest first.
        public static List<Info> List()
        {
            var list = new List<Info>();
            var folder = Folder;
            if (folder == null || !Directory.Exists(folder))
                return list;
            foreach (var f in Directory.GetFiles(folder, "*" + Ext))
            {
                var info = ReadInfo(f);
                if (info != null)
                    list.Add(info);
            }
            list.Sort((a, b) => b.Created.CompareTo(a.Created));
            return list;
        }

        /// Give a snapshot file a new name (the name is in its header; the state is copied over unchanged). False if it failed.
        public static bool Rename(string path, string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0)
                return false;
            try
            {
                var data = File.ReadAllBytes(path);
                var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
                if (r.ReadString() != Magic || r.ReadInt32() != Format)
                    return false;
                r.ReadString();
                int rest = (int)r.BaseStream.Position;
                var ms = new MemoryStream();
                var w = new BinaryWriter(ms, Encoding.UTF8);
                w.Write(Magic);
                w.Write(Format);
                w.Write(name);
                w.Write(data, rest, data.Length - rest);
                w.Flush();
                string tmp = path + ".tmp";
                File.WriteAllBytes(tmp, ms.ToArray());
                File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string LoadFile(Game g, string path, out string notes)
        {
            notes = "";
            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch (Exception e) { return "Can't open the file: " + e.Message; }
            return Restore(g, data, out notes);
        }

        // --- Which Game fields are kept --------------------------------------------------------------------------

        static Dictionary<string, FieldInfo> gameFields;

        /// The Game's fields a snapshot keeps, by name.
        internal static Dictionary<string, FieldInfo> GameFields()
        {
            if (gameFields != null)
                return gameFields;
            gameFields = new Dictionary<string, FieldInfo>();
            foreach (var f in Fields(typeof(Game)))
                if (!Skip(f))
                    gameFields[NameOf(f)] = f;
            return gameFields;
        }

        /// Left as they are on the live game: the network, hooks and dev toggles.
        static bool Skip(FieldInfo f)
        {
            var t = f.FieldType;
            if (typeof(Delegate).IsAssignableFrom(t) || t == typeof(Session)
                || t == typeof(LagStats) || t == typeof(System.Diagnostics.Stopwatch)
                || typeof(System.IO.Stream).IsAssignableFrom(t) || t == typeof(System.IO.BinaryWriter))  // (SendSnapshot's reused buffer)
                return true;
            switch (f.Name)
            {
                case "Bot":
                case "BotLevel":
                case "Sounds":
                    return true;
            }
            return false;
        }

        // --- Shared helpers --------------------------------------------------------------------------------------

        static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();

        /// Every instance field of a type and its bases, in a fixed order.
        internal static FieldInfo[] Fields(Type t)
        {
            FieldInfo[] list;
            if (fieldCache.TryGetValue(t, out list))
                return list;
            var all = new List<FieldInfo>();
            for (var b = t; b != null && b != typeof(object) && b != typeof(ValueType); b = b.BaseType)
                foreach (var f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    all.Add(f);
            list = all.ToArray();
            fieldCache[t] = list;
            return list;
        }

        /// A field's name in the file: its own name, with its declaring class for a base class's field.
        internal static string NameOf(FieldInfo f) => f.DeclaringType == f.ReflectedType ? f.Name : f.DeclaringType.Name + "." + f.Name;

        /// Things that can't or shouldn't be copied (sockets, threads, clocks, engine objects): written as null.
        internal static bool Unsupported(Type t)
        {
            string ns = t.Namespace ?? "";
            return ns.StartsWith("System.Net") || ns.StartsWith("System.Threading") || ns.StartsWith("System.Diagnostics") || ns.StartsWith("System.Reflection")
                || ns.StartsWith("UnityEngine") || typeof(Stream).IsAssignableFrom(t) || typeof(Type).IsAssignableFrom(t) || t == typeof(IntPtr) || t == typeof(UIntPtr)
                || t.IsPointer;
        }

        /// A value read back, made to fit its field (an int written for what is now a float, ...); throws when it can't.
        internal static object Fit(object v, Type t)
        {
            if (v == null || t.IsInstanceOfType(v))
                return v;
            var u = Nullable.GetUnderlyingType(t) ?? t;
            if (u.IsInstanceOfType(v))
                return v;
            if (u.IsEnum && v is IConvertible)
                return Enum.ToObject(u, Convert.ToInt64(v));
            if (u.IsPrimitive && v is IConvertible)
                return Convert.ChangeType(v, u, System.Globalization.CultureInfo.InvariantCulture);
            throw new InvalidCastException(v.GetType().Name + " -> " + t.Name);
        }

        // Objects reachable from static fields (definitions, tables): kept by reference, written as a path.
        static Dictionary<object, string> sharedPaths;
        static Dictionary<string, object> sharedObjects;

        internal static Dictionary<object, string> SharedPaths()
        {
            if (sharedPaths == null)
                BuildShared();
            return sharedPaths;
        }

        internal static Dictionary<string, object> SharedObjects()
        {
            if (sharedObjects == null)
                BuildShared();
            return sharedObjects;
        }

        static void BuildShared()
        {
            sharedPaths = new Dictionary<object, string>(RefEq.Instance);
            sharedObjects = new Dictionary<string, object>();
            foreach (var t in typeof(Game).Assembly.GetTypes())
            {
                if (t.Namespace != "Arena" || t.ContainsGenericParameters || t == typeof(RunSnapshot))
                    continue;
                foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsLiteral || !MaySharedArena(f.FieldType) || f.Name.Contains("<"))
                        continue;
                    object v;
                    try { v = f.GetValue(null); }
                    catch (Exception) { continue; }
                    string root = t.FullName + ":" + f.Name;
                    Share(v, root);
                    if (v is IDictionary d)
                    {
                        int i = 0;
                        foreach (DictionaryEntry e in d)
                        {
                            Share(e.Key, root + "$k" + i);
                            Share(e.Value, root + "$v" + i);
                            i++;
                        }
                    }
                    else if (v is IEnumerable seq && !(v is string))
                    {
                        int i = 0;
                        foreach (var e in seq)
                            Share(e, root + "#" + i++);
                    }
                }
            }
        }

        static void Share(object v, string path)
        {
            if (v == null || v is string || v is Game || v.GetType().IsValueType || v is Delegate || Unsupported(v.GetType()) || sharedPaths.ContainsKey(v))
                return;
            sharedPaths[v] = path;
            sharedObjects[path] = v;
        }

        /// Could a static field of this type hold Arena objects worth sharing (a class, or a collection of them)?
        static bool MaySharedArena(Type t)
        {
            if (t.IsArray)
                return MaySharedArena(t.GetElementType());
            if (t.IsGenericType)
            {
                foreach (var a in t.GetGenericArguments())
                    if (MaySharedArena(a))
                        return true;
                return false;
            }
            return t.IsClass && t.Namespace == "Arena" && t != typeof(Game) && !typeof(Delegate).IsAssignableFrom(t);
        }

        internal sealed class RefEq : IEqualityComparer<object>
        {
            public static readonly RefEq Instance = new RefEq();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }

        // Value tags in the stream.
        internal const byte TNull = 0, TRef = 1, TRoot = 2, TShared = 3, TPrim = 4, TEnum = 5, TObj = 6, TStruct = 7;
        // How an object's contents are written.
        internal const byte KFields = 0, KArray = 1, KList = 2, KSet = 3, KDict = 4, KStack = 5, KQueue = 6, KDelegate = 7, KText = 8;

        internal static byte KindOf(Type t)
        {
            if (t.IsArray) return KArray;
            if (typeof(Delegate).IsAssignableFrom(t)) return KDelegate;
            if (t == typeof(StringBuilder)) return KText;
            if (t.IsGenericType)
            {
                var d = t.GetGenericTypeDefinition();
                if (d == typeof(List<>) || d == typeof(LinkedList<>)) return KList;
                if (d == typeof(HashSet<>) || d == typeof(SortedSet<>)) return KSet;
                if (d == typeof(Dictionary<,>) || d == typeof(SortedDictionary<,>) || d == typeof(SortedList<,>)) return KDict;
                if (d == typeof(Stack<>)) return KStack;
                if (d == typeof(Queue<>)) return KQueue;
            }
            return KFields;
        }

        /// A collection's comparer, or null when it's the default (so it's not written).
        internal static object ComparerOf(object coll)
        {
            var p = coll.GetType().GetProperty("Comparer", BindingFlags.Instance | BindingFlags.Public);
            if (p == null)
                return null;
            var c = p.GetValue(coll, null);
            if (c == null)
                return null;
            var args = coll.GetType().GetGenericArguments();
            var eqDef = typeof(EqualityComparer<>).MakeGenericType(args[0]).GetProperty("Default").GetValue(null, null);
            var cmpDef = typeof(Comparer<>).MakeGenericType(args[0]).GetProperty("Default").GetValue(null, null);
            return ReferenceEquals(c, eqDef) || ReferenceEquals(c, cmpDef) || c.GetType().Namespace == "System.Collections.Generic" ? null : c;
        }

        internal static string Signature(MethodInfo m)
        {
            var sb = new StringBuilder();
            foreach (var p in m.GetParameters())
                sb.Append(p.ParameterType.Name).Append(',');
            return sb.ToString();
        }
    }

    /// Writes an object graph (RunSnapshot's format): types once, each object once (later uses are references).
    sealed class GraphWriter
    {
        readonly BinaryWriter w;
        readonly Game root;
        /// Re-adds a Dictionary's entries in the same order, so the live one has no free slots left by removals: a restored
        /// copy (built fresh) then places later additions exactly where the live one does, and both iterate the same way
        /// (e.g. which of two bullets hits an enemy first). Sorted kinds keep their own order and are left alone.
        static void Compact(IDictionary d)
        {
            var t = d.GetType();
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(Dictionary<,>) || d.Count == 0)
                return;
            var keys = new object[d.Count];
            var vals = new object[d.Count];
            int n = 0;
            foreach (DictionaryEntry e in d) { keys[n] = e.Key; vals[n] = e.Value; n++; }
            d.Clear();
            for (int k = 0; k < n; k++)
                d.Add(keys[k], vals[k]);
        }

        readonly Dictionary<object, int> ids = new Dictionary<object, int>(RunSnapshot.RefEq.Instance);
        readonly Dictionary<Type, int> types = new Dictionary<Type, int>();
        readonly Dictionary<object, string> shared = RunSnapshot.SharedPaths();

        public GraphWriter(BinaryWriter w, Game root)
        {
            this.w = w;
            this.root = root;
        }

        public void WriteRoot()
        {
            var fields = RunSnapshot.GameFields();
            w.Write(fields.Count);
            foreach (var kv in fields)
            {
                w.Write(kv.Key);
                Value(kv.Value.GetValue(root));
            }
        }

        void TypeRef(Type t)
        {
            int id;
            if (types.TryGetValue(t, out id))
            {
                w.Write(id);
                return;
            }
            id = types.Count;
            types[t] = id;
            w.Write(-1);
            if (t.IsArray)
            {
                w.Write((byte)1);
                w.Write(t.GetArrayRank());
                TypeRef(t.GetElementType());
            }
            else if (t.IsGenericType)
            {
                w.Write((byte)2);
                w.Write(t.GetGenericTypeDefinition().FullName);
                var args = t.GetGenericArguments();
                w.Write(args.Length);
                foreach (var a in args)
                    TypeRef(a);
            }
            else
            {
                w.Write((byte)0);
                w.Write(t.FullName);
            }
            if (!t.IsPrimitive && !t.IsEnum && RunSnapshot.KindOf(t) == RunSnapshot.KFields)
            {
                var fs = RunSnapshot.Fields(t);
                w.Write(fs.Length);
                foreach (var f in fs)
                    w.Write(RunSnapshot.NameOf(f));
            }
        }

        void Value(object v)
        {
            if (v == null)
            {
                w.Write(RunSnapshot.TNull);
                return;
            }
            var t = v.GetType();
            if (Prim(v, t))
                return;
            if (t.IsEnum)
            {
                w.Write(RunSnapshot.TEnum);
                TypeRef(t);
                w.Write(Convert.ToInt64(v));
                return;
            }
            if (RunSnapshot.Unsupported(t))
            {
                w.Write(RunSnapshot.TNull);
                return;
            }
            if (t.IsValueType)
            {
                w.Write(RunSnapshot.TStruct);
                TypeRef(t);
                foreach (var f in RunSnapshot.Fields(t))
                    Value(f.GetValue(v));
                return;
            }
            if (ReferenceEquals(v, root))
            {
                w.Write(RunSnapshot.TRoot);
                return;
            }
            int id;
            if (ids.TryGetValue(v, out id))
            {
                w.Write(RunSnapshot.TRef);
                w.Write(id);
                return;
            }
            string path;
            if (shared.TryGetValue(v, out path))
            {
                w.Write(RunSnapshot.TShared);
                w.Write(path);
                return;
            }
            ids[v] = ids.Count;
            w.Write(RunSnapshot.TObj);
            TypeRef(t);
            Contents(v, t);
        }

        void Contents(object v, Type t)
        {
            switch (RunSnapshot.KindOf(t))
            {
                case RunSnapshot.KArray:
                {
                    var a = (Array)v;
                    for (int d = 0; d < a.Rank; d++)
                        w.Write(a.GetLength(d));
                    foreach (var e in a)
                        Value(e);
                    break;
                }
                case RunSnapshot.KText:
                    w.Write(v.ToString());
                    break;
                case RunSnapshot.KList:
                case RunSnapshot.KStack:
                case RunSnapshot.KQueue:
                case RunSnapshot.KSet:
                {
                    if (RunSnapshot.KindOf(t) == RunSnapshot.KSet)
                        Value(RunSnapshot.ComparerOf(v));
                    var items = new List<object>();
                    foreach (var e in (IEnumerable)v)
                        items.Add(e);
                    w.Write(items.Count);
                    foreach (var e in items)
                        Value(e);
                    break;
                }
                case RunSnapshot.KDict:
                {
                    Value(RunSnapshot.ComparerOf(v));
                    var d = (IDictionary)v;
                    Compact(d);
                    w.Write(d.Count);
                    foreach (DictionaryEntry e in d)
                    {
                        Value(e.Key);
                        Value(e.Value);
                    }
                    break;
                }
                case RunSnapshot.KDelegate:
                {
                    var list = ((Delegate)v).GetInvocationList();
                    w.Write(list.Length);
                    foreach (var d in list)
                    {
                        var m = d.Method;
                        TypeRef(m.DeclaringType);
                        w.Write(m.Name);
                        w.Write(RunSnapshot.Signature(m));
                        Value(d.Target);
                    }
                    break;
                }
                default:
                    foreach (var f in RunSnapshot.Fields(t))
                        Value(f.GetValue(v));
                    break;
            }
        }

        bool Prim(object v, Type t)
        {
            var code = Type.GetTypeCode(t);
            if (t.IsEnum || code == TypeCode.Object || code == TypeCode.Empty || code == TypeCode.DBNull)
                return false;
            w.Write(RunSnapshot.TPrim);
            w.Write((byte)code);
            switch (code)
            {
                case TypeCode.Boolean: w.Write((bool)v); break;
                case TypeCode.Char: w.Write((char)v); break;
                case TypeCode.SByte: w.Write((sbyte)v); break;
                case TypeCode.Byte: w.Write((byte)v); break;
                case TypeCode.Int16: w.Write((short)v); break;
                case TypeCode.UInt16: w.Write((ushort)v); break;
                case TypeCode.Int32: w.Write((int)v); break;
                case TypeCode.UInt32: w.Write((uint)v); break;
                case TypeCode.Int64: w.Write((long)v); break;
                case TypeCode.UInt64: w.Write((ulong)v); break;
                case TypeCode.Single: w.Write((float)v); break;
                case TypeCode.Double: w.Write((double)v); break;
                case TypeCode.Decimal: w.Write((decimal)v); break;
                case TypeCode.DateTime: w.Write(((DateTime)v).ToBinary()); break;
                case TypeCode.String: w.Write((string)v); break;
            }
            return true;
        }
    }

    /// Reads what GraphWriter wrote.
    sealed class GraphReader
    {
        readonly BinaryReader r;
        readonly Game root;
        readonly List<object> objs = new List<object>();
        readonly List<TypeEntry> types = new List<TypeEntry>();
        public int Dropped;  // values that couldn't be put back (a delegate's method is gone, a field changed type)

        sealed class TypeEntry
        {
            public Type Type;
            public FieldInfo[] Fields;  // in the file's order; null for a field that's gone
        }

        public GraphReader(BinaryReader r, Game root)
        {
            this.r = r;
            this.root = root;
        }

        public Dictionary<string, object> ReadRoot()
        {
            var result = new Dictionary<string, object>();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                string name = r.ReadString();
                result[name] = Value();
            }
            return result;
        }

        TypeEntry TypeRef()
        {
            int id = r.ReadInt32();
            if (id >= 0)
                return types[id];
            var e = new TypeEntry();
            types.Add(e);
            byte kind = r.ReadByte();
            if (kind == 1)
            {
                int rank = r.ReadInt32();
                var elem = TypeRef().Type;
                e.Type = rank == 1 ? elem.MakeArrayType() : elem.MakeArrayType(rank);
            }
            else if (kind == 2)
            {
                var def = Resolve(r.ReadString());
                var args = new Type[r.ReadInt32()];
                for (int i = 0; i < args.Length; i++)
                    args[i] = TypeRef().Type;
                e.Type = def.MakeGenericType(args);
            }
            else
                e.Type = Resolve(r.ReadString());
            var t = e.Type;
            if (!t.IsPrimitive && !t.IsEnum && RunSnapshot.KindOf(t) == RunSnapshot.KFields)
            {
                var have = new Dictionary<string, FieldInfo>();
                foreach (var f in RunSnapshot.Fields(t))
                    have[RunSnapshot.NameOf(f)] = f;
                e.Fields = new FieldInfo[r.ReadInt32()];
                for (int i = 0; i < e.Fields.Length; i++)
                {
                    FieldInfo f;
                    have.TryGetValue(r.ReadString(), out f);
                    e.Fields[i] = f;
                }
            }
            return e;
        }

        static readonly Dictionary<string, Type> resolved = new Dictionary<string, Type>();

        /// The assemblies a saved type can live in (game, mscorlib, System, System.Core): a fixed list, not
        /// AppDomain.GetAssemblies, which Unity warns about (it can return unloaded assemblies).
        static readonly Assembly[] TypeHomes =
        {
            typeof(Game).Assembly, typeof(object).Assembly, typeof(Uri).Assembly, typeof(HashSet<int>).Assembly,
        };

        static Type Resolve(string name)
        {
            Type t;
            if (resolved.TryGetValue(name, out t))
                return t;
            t = typeof(Game).Assembly.GetType(name) ?? Type.GetType(name);
            if (t == null)
                foreach (var a in TypeHomes)
                    if ((t = a.GetType(name)) != null)
                        break;
            if (t == null)
                throw new InvalidDataException("type " + name + " no longer exists");
            resolved[name] = t;
            return t;
        }

        object Value()
        {
            byte tag = r.ReadByte();
            switch (tag)
            {
                case RunSnapshot.TNull: return null;
                case RunSnapshot.TRoot: return root;
                case RunSnapshot.TRef: return objs[r.ReadInt32()];
                case RunSnapshot.TShared:
                {
                    string path = r.ReadString();
                    object o;
                    if (!RunSnapshot.SharedObjects().TryGetValue(path, out o))
                        throw new InvalidDataException("shared object " + path + " no longer exists");
                    return o;
                }
                case RunSnapshot.TPrim: return Prim((TypeCode)r.ReadByte());
                case RunSnapshot.TEnum:
                {
                    var t = TypeRef().Type;
                    return Enum.ToObject(t, r.ReadInt64());
                }
                case RunSnapshot.TStruct:
                {
                    var e = TypeRef();
                    object box = Activator.CreateInstance(e.Type);
                    FillFields(box, e);
                    return box;
                }
                case RunSnapshot.TObj: return Obj(TypeRef());
            }
            throw new InvalidDataException("bad tag " + tag);
        }

        void FillFields(object o, TypeEntry e)
        {
            foreach (var f in e.Fields)
            {
                var v = Value();
                if (f == null)
                    continue;
                try { f.SetValue(o, RunSnapshot.Fit(v, f.FieldType)); }
                catch (Exception) { Dropped++; }
            }
        }

        object Obj(TypeEntry e)
        {
            var t = e.Type;
            int id = objs.Count;
            objs.Add(null);
            switch (RunSnapshot.KindOf(t))
            {
                case RunSnapshot.KArray:
                {
                    int rank = t.GetArrayRank();
                    var lens = new int[rank];
                    for (int d = 0; d < rank; d++)
                        lens[d] = r.ReadInt32();
                    var a = Array.CreateInstance(t.GetElementType(), lens);
                    objs[id] = a;
                    var idx = new int[rank];
                    int total = a.Length;
                    var elem = t.GetElementType();
                    for (int n = 0; n < total; n++)
                    {
                        var v = Value();
                        try { a.SetValue(RunSnapshot.Fit(v, elem), idx); }
                        catch (Exception) { Dropped++; }
                        for (int d = rank - 1; d >= 0; d--)  // row-major, as foreach wrote them
                        {
                            if (++idx[d] < lens[d])
                                break;
                            idx[d] = 0;
                        }
                    }
                    return a;
                }
                case RunSnapshot.KText:
                {
                    var sb = new StringBuilder(r.ReadString());
                    objs[id] = sb;
                    return sb;
                }
                case RunSnapshot.KList:
                case RunSnapshot.KStack:
                case RunSnapshot.KQueue:
                case RunSnapshot.KSet:
                {
                    byte kind = RunSnapshot.KindOf(t);
                    object comparer = kind == RunSnapshot.KSet ? Value() : null;
                    object c = comparer != null ? Activator.CreateInstance(t, comparer) : Activator.CreateInstance(t);
                    objs[id] = c;
                    int n = r.ReadInt32();
                    var items = new object[n];
                    var elem = t.GetGenericArguments()[0];
                    for (int i = 0; i < n; i++)
                        items[i] = Value();
                    string add = kind == RunSnapshot.KStack ? "Push" : kind == RunSnapshot.KQueue ? "Enqueue" : kind == RunSnapshot.KList && t.GetGenericTypeDefinition() == typeof(LinkedList<>) ? "AddLast" : "Add";
                    var m = t.GetMethod(add, new[] { elem });
                    if (kind == RunSnapshot.KStack)
                        Array.Reverse(items);  // a stack lists top first
                    var arg = new object[1];
                    foreach (var v in items)
                    {
                        try
                        {
                            arg[0] = RunSnapshot.Fit(v, elem);
                            m.Invoke(c, arg);
                        }
                        catch (Exception) { Dropped++; }
                    }
                    return c;
                }
                case RunSnapshot.KDict:
                {
                    var comparer = Value();
                    var d = (IDictionary)(comparer != null ? Activator.CreateInstance(t, comparer) : Activator.CreateInstance(t));
                    objs[id] = d;
                    var args = t.GetGenericArguments();
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        var k = Value();
                        var v = Value();
                        try { d[RunSnapshot.Fit(k, args[0])] = RunSnapshot.Fit(v, args[1]); }
                        catch (Exception) { Dropped++; }
                    }
                    return d;
                }
                case RunSnapshot.KDelegate:
                {
                    int n = r.ReadInt32();
                    Delegate all = null;
                    for (int i = 0; i < n; i++)
                    {
                        var owner = TypeRef().Type;
                        string name = r.ReadString(), sig = r.ReadString();
                        var target = Value();
                        MethodInfo method = null;
                        foreach (var m in owner.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                            if (m.Name == name && RunSnapshot.Signature(m) == sig)
                                method = m;
                        Delegate d = null;
                        if (method != null)
                            try { d = method.IsStatic ? Delegate.CreateDelegate(t, method) : Delegate.CreateDelegate(t, target, method); }
                            catch (Exception) { }  // (counted in Dropped just below)
                        if (d == null)
                            Dropped++;
                        all = Delegate.Combine(all, d);
                    }
                    objs[id] = all;
                    return all;
                }
                default:
                {
                    object o = New(t);
                    objs[id] = o;
                    FillFields(o, e);
                    return o;
                }
            }
        }

        /// A fresh object: its parameterless constructor when it has one (so fields added since the snapshot get their
        /// usual starting values), else a blank one.
        static object New(Type t)
        {
            var ctor = t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ctor != null)
                try { return ctor.Invoke(null); }
                catch (Exception) { }  // (a constructor that throws: the blank object below instead)
            return FormatterServices.GetUninitializedObject(t);
        }

        object Prim(TypeCode code)
        {
            switch (code)
            {
                case TypeCode.Boolean: return r.ReadBoolean();
                case TypeCode.Char: return r.ReadChar();
                case TypeCode.SByte: return r.ReadSByte();
                case TypeCode.Byte: return r.ReadByte();
                case TypeCode.Int16: return r.ReadInt16();
                case TypeCode.UInt16: return r.ReadUInt16();
                case TypeCode.Int32: return r.ReadInt32();
                case TypeCode.UInt32: return r.ReadUInt32();
                case TypeCode.Int64: return r.ReadInt64();
                case TypeCode.UInt64: return r.ReadUInt64();
                case TypeCode.Single: return r.ReadSingle();
                case TypeCode.Double: return r.ReadDouble();
                case TypeCode.Decimal: return r.ReadDecimal();
                case TypeCode.DateTime: return DateTime.FromBinary(r.ReadInt64());
                case TypeCode.String: return r.ReadString();
            }
            throw new InvalidDataException("bad value type " + code);
        }
    }

    public partial class Game
    {
        /// Tidy-up after RunSnapshot.Restore: drop timed actions whose code changed since, rebuild the prop grid, no sounds from
        /// before, and the run no longer counts for records.
        internal void AfterSnapshotLoad()
        {
            katanaLater.RemoveAll(l => l.Run == null);
            System.Threading.Interlocked.Increment(ref PropStamp);
            Sounds.Clear();
            DevUsed("loaded a run snapshot");
        }
    }
}

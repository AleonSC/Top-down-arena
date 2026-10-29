using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The dev text editor's overrides.
static partial class Program
{
    // The dev text editor's overrides: exact text, numbers kept by the game, the file round trip.
    static void TextOverridesTest()
    {
        TextOverrides.Clear();
        TextOverrides.Set("Class", "Character class");
        TextOverrides.Set("Pay 15% of your max health", "Spend 15% of your max health");
        Check(TextOverrides.Apply("Class") == "Character class" && TextOverrides.Apply("Classes") == "Classes", "an edited text shows in its place (only that exact text)");
        Check(TextOverrides.Apply("Pay 40% of your max health") == "Spend 40% of your max health", "an edit keeps the game's numbers (one edit covers every number)");
        Check(TextOverrides.Problem("Wave 6 / 50", "Round 6") != null && TextOverrides.Problem("Wave 6 / 50", "Round 6 of 50") == null
            && TextOverrides.Problem("Class", "Class 2") != null && TextOverrides.Problem("Class", " ") != null, "an edit must keep the same count of numbers and can't be empty");
        TextOverrides.Set("Two\nlines\twith tab\\", "Ok\nthen");
        string file = TextOverrides.Serialize();
        TextOverrides.Clear();
        TextOverrides.Parse(file);
        Check(TextOverrides.Count == 3 && TextOverrides.Apply("Two\nlines\twith tab\\") == "Ok\nthen" && TextOverrides.Apply("Class") == "Character class",
            "the overrides file keeps every edit (new lines, tabs and backslashes too)");
        TextOverrides.Set("Class", "Class");
        Check(!TextOverrides.Has("Class") && TextOverrides.Apply("Class") == "Class", "setting a text back to the original removes the edit");
        TextOverrides.Clear();
    }
}

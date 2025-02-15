using Mirror;
using System.Collections.Generic;

public static class MirrorSerializationExtensions
{
    // Writer for KeyValuePair<string, int>
    public static void WriteKeyValuePairStringInt(this NetworkWriter writer, KeyValuePair<string, int> pair)
    {
        writer.WriteString(pair.Key); // Write the string key
        writer.WriteInt(pair.Value); // Write the int value
    }

    // Reader for KeyValuePair<string, int>
    public static KeyValuePair<string, int> ReadKeyValuePairStringInt(this NetworkReader reader)
    {
        string key = reader.ReadString(); // Read the string key
        int value = reader.ReadInt();    // Read the int value
        return new KeyValuePair<string, int>(key, value);
    }

    // Writer for List<KeyValuePair<string, int>>
    public static void WriteKeyValuePairList(this NetworkWriter writer, List<KeyValuePair<string, int>> list)
    {
        writer.WriteInt(list.Count); // Write the count of items
        foreach (var pair in list)
        {
            writer.WriteKeyValuePairStringInt(pair); // Write each KeyValuePair
        }
    }

    // Reader for List<KeyValuePair<string, int>>
    public static List<KeyValuePair<string, int>> ReadKeyValuePairList(this NetworkReader reader)
    {
        int count = reader.ReadInt(); // Read the count of items
        var list = new List<KeyValuePair<string, int>>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(reader.ReadKeyValuePairStringInt()); // Read and add each KeyValuePair
        }
        return list;
    }
}

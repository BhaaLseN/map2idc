using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace map2idc
{
    class Program
    {
        static void Main(string[] args)
        {
            string mapFile = args.Length >= 1 ? args[0] : @"D:\Dolphin\windwaker\GZLE01.map";

            string[] lines = File.ReadAllLines(mapFile);
            var sections = SplitSections(lines);

            string idcFile = args.Length >= 2 ? args[1] : Path.ChangeExtension(mapFile, ".idc");

            if (File.Exists(idcFile))
                File.Delete(idcFile);

            using (var sw = new StreamWriter(File.OpenWrite(idcFile)))
            {
                WriteMain(sw);
                WriteSegments(sw, sections);
                WriteFunctions(sw, sections);
            }
        }
        private static void WriteMain(StreamWriter outFile)
        {
            outFile.WriteLine("static main()");
            outFile.WriteLine("{");

            //write functions
            outFile.WriteLine("\thandleSegments();");
            outFile.WriteLine("\thandleFunctions();");

            outFile.WriteLine("}");
        }
        private static void WriteSegments(StreamWriter outFile, IDictionary<string, string[]> sections)
        {
            outFile.WriteLine("static handleSegments()");
            outFile.WriteLine("{");

            //rename segments
            string[] segmentLines;
            if (sections.TryGetValue("Memory map:", out segmentLines))
            {
                var segments = new List<string>();
                foreach (string line in segmentLines)
                {
                    var match = Regex.Match(line, @"\s+(?<name>[^ ]+)\s+(?<start>[a-f0-9]+)\s+(?<size>[a-f0-9]+)\s+(?<offset>[a-f0-9]+)", RegexOptions.Compiled);
                    if (match.Success)
                    {
                        outFile.WriteLine("\tRenameSeg(0x{0}, \"{1}\");", match.Groups["start"].Value, match.Groups["name"].Value);
                        segments.Add(match.Groups["name"].Value.Trim());
                    }
                }
                sections["segments"] = segments.ToArray();
            }

            outFile.WriteLine("}");
        }
        private static void WriteFunctions(StreamWriter outFile, IDictionary<string, string[]> sections)
        {
            outFile.WriteLine("static handleFunctions()");
            outFile.WriteLine("{");

            //rename functions
            string[] segments;
            if (sections.TryGetValue("segments", out segments))
            {
                foreach (string segment in segments)
                {
                    string[] segmentLines;
                    if (sections.TryGetValue(string.Format("{0} section layout", segment), out segmentLines))
                    {
                        foreach (string line in segmentLines)
                        {
                            var match = Regex.Match(line, @"\s+(?<start>[a-f0-9]+)\s+(?<size>[a-f0-9]+)\s+(?<address>[a-f0-9]+)\s+(?<type>\d+)\s+(?<name>[^ ]+)\s*(?<module>.+)?", RegexOptions.Compiled);
                            if (match.Success)
                            {
                                //type 1 == segment?
                                //type 4 == symbol?
                                if (match.Groups["type"].Value == "4")
                                {
                                    string name = match.Groups["name"].Value.Trim();
                                    int flags = 0x101;
                                    if (name[0] == '@')
                                        flags += 0x04 + 0x20;
                                    else
                                        flags += 0x02 + 0x40;

                                    outFile.WriteLine("\tMakeNameEx(0x{0}, \"{1}\", 0x{2:x});", match.Groups["address"].Value, name, flags);
                                    if (match.Groups["module"].Success)
                                        outFile.WriteLine("\tMakeComm(0x{0}, \"{1}\");", match.Groups["address"].Value, match.Groups["module"].Value.Trim());
                                }
                            }
                        }
                    }
                }
            }

            outFile.WriteLine("}");
        }
        private static IDictionary<string, string[]> SplitSections(string[] lines)
        {
            var ret = new Dictionary<string, string[]>();
            var currentSegmentEntries = new List<string>();
            string currentSegment = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    if (currentSegmentEntries.Any())
                    {
                        ret[currentSegment] = currentSegmentEntries.ToArray();
                        currentSegmentEntries.Clear();
                    }
                }
                else if (line[0] != ' ')
                {
                    currentSegment = line.Trim();
                }
                else
                {
                    currentSegmentEntries.Add(line);
                }
            }

            if (currentSegmentEntries.Any())
            {
                ret[currentSegment] = currentSegmentEntries.ToArray();
            }

            return ret;
        }
    }
}

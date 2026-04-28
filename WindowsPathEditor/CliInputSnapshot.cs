using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace WindowsPathEditor
{
    [DataContract]
    internal sealed class CliInputSnapshot
    {
        [DataMember(Name = "systemPath", Order = 1)]
        public List<string> SystemPath { get; set; }

        [DataMember(Name = "userPath", Order = 2)]
        public List<string> UserPath { get; set; }

        public static CliInputSnapshot Load(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(CliInputSnapshot));
                var snapshot = (CliInputSnapshot)serializer.ReadObject(stream);
                if (snapshot == null)
                {
                    return new CliInputSnapshot();
                }

                snapshot.SystemPath = Sanitize(snapshot.SystemPath);
                snapshot.UserPath = Sanitize(snapshot.UserPath);
                return snapshot;
            }
        }

        private static List<string> Sanitize(IEnumerable<string> entries)
        {
            return (entries ?? Enumerable.Empty<string>())
                .Where(entry => !string.IsNullOrEmpty(entry))
                .ToList();
        }
    }
}

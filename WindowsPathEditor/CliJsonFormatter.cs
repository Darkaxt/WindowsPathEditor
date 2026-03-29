using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace WindowsPathEditor
{
    internal static class CliJsonFormatter
    {
        public static string FormatPaths(CliPathsPayload payload)
        {
            return Serialize(payload);
        }

        public static string FormatCleanup(CliCleanupPayload payload)
        {
            return Serialize(payload);
        }

        public static string FormatConflicts(CliConflictsPayload payload)
        {
            return Serialize(payload);
        }

        public static string FormatAutosort(CliAutosortPayload payload)
        {
            return Serialize(payload);
        }

        public static string FormatMigrate(CliMigratePayload payload)
        {
            return Serialize(payload);
        }

        public static string FormatScan(CliScanPayload payload)
        {
            return Serialize(payload);
        }

        private static string Serialize<T>(T payload)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, payload);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}

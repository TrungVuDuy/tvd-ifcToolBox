using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace IfcToolbox.Core.Utilities
{
    public class ReflectionUtility
    {
        /// <summary>
        /// Directory the application runs from.
        /// Uses AppContext.BaseDirectory rather than Assembly.CodeBase, which is obsolete
        /// since .NET 5 (SYSLIB0012) and unsupported in single-file deployments.
        /// </summary>
        public static string GetExecutingAssemblyDirectory => AppContext.BaseDirectory;

        /// <summary>
        /// Writes an embedded binary next to the application, unless it is already there.
        /// Returns the full path, or null when the assembly carries no such resource.
        /// </summary>
        public static string CopyEmbeddedResource(Assembly sourceAssembly, string fileName)
        {
            string path = Path.Combine(GetExecutingAssemblyDirectory, fileName);
            if (File.Exists(path))
                return path;

            var resourceName = sourceAssembly.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith(fileName));
            if (resourceName == null)
                return null;

            using var stream = sourceAssembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;
            CopyBinaryFileToAssemblyDirectory(fileName, stream);
            return path;
        }

        public static void CopyBinaryFileToAssemblyDirectory(string filename, Stream stream)
        {
            string path = Path.Combine(GetExecutingAssemblyDirectory, filename);
            if (File.Exists(path)) return;
            using (var file = File.Create(path))
                stream.CopyTo(file);
        }

        public static Dictionary<string, object> DictionaryFromType(object atype)
        {
            if (atype == null) return new Dictionary<string, object>();
            PropertyInfo[] props;
            if (atype is Type t)
                props = t.GetProperties();
            else
                props = atype.GetType().GetProperties();
            Dictionary<string, object> dict = new Dictionary<string, object>();
            foreach (PropertyInfo prop in props)
            {
                if (atype is Type)
                {
                    dict.Add(prop.Name, string.Empty);
                    continue;
                }
                object value = prop.GetValue(atype, new object[] { });
                dict.Add(prop.Name, value);
            }
            return dict;
        }

    }
}

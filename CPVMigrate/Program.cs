using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace CPVMigrate
{
    class Program
    {
        private static int totalProjects = 0;

        private static List<string> projects = new List<string>();

        static void Main(string[] args)
        {
            Console.WriteLine(" -- Central Package Version Migration Tool -- ");
            ProcessProjects(args[0]);
        }

        public static void ProcessProjects(string rootFilePath)
        {
            var packages = new HashSet<NuGetPackage>();

            foreach (var file in Directory.EnumerateFiles(rootFilePath, "*.csproj", SearchOption.AllDirectories))
            {
                totalProjects++;
                projects.Add(file);

                Console.WriteLine($"Processing {file}...");

                var currentPackagesWithVersionAttribute = CollectPackages(file).ToList();

                if (currentPackagesWithVersionAttribute.Count > 0)
                {
                    // Remove the packages from .csproj file only if it's collected in previous step.
                    // Else, don't remove.  Just removing the packages without collecting the pacakges will be misleading.
                    RemovePackageVersions(file); 
                    foreach (var p in currentPackagesWithVersionAttribute)
                    {
                        packages.Add(p);
                    }
                }
            }

            WritePropsFile(packages, rootFilePath);

            Console.WriteLine($"--------Processing Completed------------");
            Console.WriteLine($"Total Projects Processed: {totalProjects}...");
            Console.WriteLine($"Processed projects are: {string.Join(Environment.NewLine, projects)}...");
            Console.ReadKey();
        }

        public static IEnumerable<NuGetPackage> CollectPackages(string csprojPath)
        {
            var packages = new List<NuGetPackage>();
            using (var stream = File.Open(csprojPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var projectXml = XDocument.Load(stream, LoadOptions.SetLineInfo);
                var packagesElements = projectXml.Root.XPathSelectElements("//PackageReference").ToList();

                if (packagesElements.Count.Equals(0))
                {
                    /* https://stackoverflow.com/a/84018408 */
                    var namespaceManager = new XmlNamespaceManager(new NameTable());
                    namespaceManager.AddNamespace("prefix", "http://schemas.microsoft.com/developer/msbuild/2003");
                    packagesElements = projectXml.Root.XPathSelectElements("//prefix:PackageReference", namespaceManager).ToList();
                }

                // This is to make the tool idempotent. To avoid exception when we run the tool again on same project again.
                var packagesWithVersion = packagesElements.Where(e => e.Attribute("Version") != null || e.Value != null).ToList();

                foreach (var element in packagesWithVersion)
                {
                    var p = new NuGetPackage()
                    {
                        Name = element.Attribute("Include").Value,

                        // element.Value is useful in case of Type-2 mentioned below. 
                        Version = element.Attribute("Version")?.Value ?? element.Value
                    };
                    packages.Add(p);
                    Console.WriteLine($"   Processing {p.Name}");
                }

                Console.WriteLine($"   Found {packagesElements.Count} Packages in total.");
                Console.WriteLine($"   Found {packagesWithVersion.Count} Packages with version attribute.");
                Console.WriteLine($"   --------------------");
            }
            return packages;
        }

        public static void RemovePackageVersions(string csprojPath)
        {
            //Type - 1
            //< PackageReference Include = "Microsoft.ClearScript.V8.Native.win-x64" Version = "7.2.4-pdk" />

            //Type - 2
            //< PackageReference Include = "Microsoft.AspNet.Razor" >
            //    < Version > 3.2.7 </ Version >
            //</ PackageReference >

            File.WriteAllText(csprojPath, Regex.Replace(File.ReadAllText(csprojPath), "(PackageReference\\s\\w.+)(\\sVersion=\"[a-zA-Z0-9\\.-]+\")", "$1"), new UTF8Encoding(true));

            // This is for type 2. Doing a blind version removal currently. Should be improved. 
            File.WriteAllText(csprojPath, Regex.Replace(File.ReadAllText(csprojPath), "(^|\\n).*<Version>\\w.+<\\/Version>.*\\n?", ""), new UTF8Encoding(true));
        }

        public static void WritePropsFile(IEnumerable<NuGetPackage> packages, string rootPath)
        {
            /*
             * <Project>
                <ItemGroup>
                    <PackageVersion Include="MSTest.TestAdapter" Version="1.1.0" />
                </ItemGroup>
                </Project>
             */
            var props = new XDocument(
                new XElement("Project",
                    new XElement("ItemGroup",
                        packages.Select(x => new XElement("PackageVersion", new XAttribute("Include", x.Name), new XAttribute("Version", x.Version))))
                    )
                );

            StreamWriter sw = new StreamWriter($"{rootPath}/Directory.packages.props");
            props.Save(sw, SaveOptions.None);
        }
    }

    public class NuGetPackage
    {
        public string Name;
        public string Version;

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as NuGetPackage;
            if (other == null)
            {
                return false;
            }
            return Name == other.Name;
        }
    }
}
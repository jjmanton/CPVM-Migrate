using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace CPVMigrate
{
    class Program
    {
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
                Console.WriteLine($"Processing {file}...");
                foreach (var p in CollectPackages(file))
                {
                    packages.Add(p);
                }

                RemovePackageVersions(file);
            }

            WritePropsFile(packages, rootFilePath);
        }

        public static IEnumerable<NuGetPackage> CollectPackages(string csprojPath)
        {
            var packages = new List<NuGetPackage>();
            XDocument projectXml;
            using (var stream = File.Open(csprojPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                projectXml = XDocument.Load(stream, LoadOptions.SetLineInfo);
                var packagesElements = projectXml.Root.XPathSelectElements("//PackageReference");
                foreach (var element in packagesElements)
                {
                    var p = new NuGetPackage()
                    {
                        Name = element.Attribute("Include").Value,
                        Version = element.Attribute("Version").Value
                    };
                    packages.Add(p);
                    Console.WriteLine($"   Processing {p.Name}");
                }
                Console.WriteLine($"   --------------------");
                Console.WriteLine($"   Found {packagesElements.Count()} Packages Total.");
            }
            return packages;
        }

        public static void RemovePackageVersions(string csprojPath)
        {
            File.WriteAllText(csprojPath, Regex.Replace(File.ReadAllText(csprojPath), "(PackageReference\\sInclude=\"[a-zA-Z0-9\\.]+\")(\\sVersion=\"[a-zA-Z0-9\\.-]+\")", "$1"), new UTF8Encoding(true));
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
            Console.WriteLine(sw);
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
            if (other == null) {
                return false;
            }
            return Name == other.Name;
        }
    }
}
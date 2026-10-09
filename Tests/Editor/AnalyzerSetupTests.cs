using NUnit.Framework;
using UnityEditor;

namespace FPM.Tests.Editor
{
    public class AnalyzerSetupTests
    {
        private const string AnalyzerFileName = "FPM.Analyzers.dll";

        // Unity applies a DLL as a Roslyn analyzer only when it carries the RoslynAnalyzer label, and it must be
        // excluded from every platform so it never ships in a build. Build and copy it with Analyzers~/build-analyzer.ps1.
        [Test]
        public void AnalyzerDll_IsSetUpAsRoslynAnalyzer()
        {
            string path = FindAnalyzerPath();
            Assert.That(path, Is.Not.Null, AnalyzerFileName + " is missing from Runtime/Analyzers. Build it with Analyzers~/build-analyzer.ps1.");

            PluginImporter importer = (PluginImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.GetCompatibleWithAnyPlatform(), Is.False, "Uncheck Any Platform for " + path);
            Assert.That(importer.GetCompatibleWithEditor(), Is.False, "Uncheck Editor for " + path);

            string[] labels = AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(path));
            Assert.That(labels, Does.Contain("RoslynAnalyzer"), "Add the RoslynAnalyzer label to " + path);
        }

        private static string FindAnalyzerPath()
        {
            foreach (string guid in AssetDatabase.FindAssets("FPM.Analyzers"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/Runtime/Analyzers/" + AnalyzerFileName))
                {
                    return path;
                }
            }
            return null;
        }
    }
}

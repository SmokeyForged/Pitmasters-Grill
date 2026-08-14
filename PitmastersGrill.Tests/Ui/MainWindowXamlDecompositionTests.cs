using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Ui
{
    public sealed class MainWindowXamlDecompositionTests
    {
        [Fact]
        public void MainWindow_UsesExtractedResourceDictionary()
        {
            var mainWindowXaml = ReadRepoFile("PitmastersGrill", "MainWindow.xaml");
            var resourcesXaml = ReadRepoFile("PitmastersGrill", "Resources", "MainWindowResources.xaml");

            Assert.Contains("ResourceDictionary Source=\"Resources/MainWindowResources.xaml\"", mainWindowXaml);
            Assert.DoesNotContain("x:Key=\"WindowBackgroundBrush\"", mainWindowXaml);
            Assert.DoesNotContain("x:Key=\"PilotBoardCellStyle\"", mainWindowXaml);

            Assert.Contains("x:Key=\"WindowBackgroundBrush\"", resourcesXaml);
            Assert.Contains("x:Key=\"PilotBoardCellStyle\"", resourcesXaml);
            Assert.Contains("x:Key=\"CompactAwareTabItemStyle\"", resourcesXaml);
            Assert.Contains("x:Key=\"PmgNestedSubTabControlStyle\"", resourcesXaml);
        }

        [Fact]
        public void AnalysisVisualTree_IsOwnedByFocusedView()
        {
            var mainWindowXaml = ReadRepoFile("PitmastersGrill", "MainWindow.xaml");
            var analysisXaml = ReadRepoFile("PitmastersGrill", "Views", "AnalysisView.xaml");
            var compatibilitySource = ReadRepoFile("PitmastersGrill", "MainWindow.AnalysisView.cs");

            Assert.Contains("<views:AnalysisView x:Name=\"AnalysisViewControl\"", mainWindowXaml);
            Assert.DoesNotContain("x:Name=\"AnalysisEmptyStateText\"", mainWindowXaml);
            Assert.DoesNotContain("x:Name=\"AnalysisAllianceListBox\"", mainWindowXaml);

            Assert.Contains("AutomationProperties.AutomationId=\"AnalysisEmptyStateText\"", analysisXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"AnalysisAllianceListBox\"", analysisXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"AnalysisCorpListBox\"", analysisXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"AnalysisCurrentCharacterText\"", analysisXaml);

            Assert.Contains("AnalysisViewControl.EmptyStateTextBlock", compatibilitySource);
            Assert.Contains("AnalysisViewControl.AllianceListBoxControl", compatibilitySource);
            Assert.Contains("AnalysisViewControl.ContextStatusTextBlock", compatibilitySource);
        }

        [Fact]
        public void AnalysisViewCodeBehind_RemainsPresentationOnly()
        {
            var source = ReadRepoFile("PitmastersGrill", "Views", "AnalysisView.xaml.cs");

            Assert.Contains("AllianceListDoubleClick", source);
            Assert.Contains("CorpListDoubleClick", source);
            Assert.DoesNotContain("new AnalysisTabController", source);
            Assert.DoesNotContain("Repository", source);
            Assert.DoesNotContain("Service", source);
            Assert.DoesNotContain("Persistence", source);
        }

        private static string ReadRepoFile(params string[] relativeSegments)
        {
            var path = GetRepoFilePath(relativeSegments);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Could not locate repository file '{path}'.", path);
            }

            return File.ReadAllText(path);
        }

        private static string GetRepoFilePath(params string[] relativeSegments)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                var projectPath = Path.Combine(current.FullName, "PitmastersGrill", "PitmastersGrill.csproj");
                if (File.Exists(projectPath))
                {
                    var pathSegments = new string[relativeSegments.Length + 1];
                    pathSegments[0] = current.FullName;
                    Array.Copy(relativeSegments, 0, pathSegments, 1, relativeSegments.Length);
                    return Path.Combine(pathSegments);
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the Pitmasters-Grill repository root from the test output directory.");
        }
    }
}

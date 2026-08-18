using PitmastersGrill.Views;
using System;
using System.IO;
using System.Threading;
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
        public void GrillVisualTree_IsOwnedByFocusedView_WithoutAbsorbingPilotDetail()
        {
            var mainWindowXaml = ReadRepoFile("PitmastersGrill", "MainWindow.xaml");
            var grillXaml = ReadRepoFile("PitmastersGrill", "Views", "GrillView.xaml");
            var compatibilitySource = ReadRepoFile("PitmastersGrill", "MainWindow.GrillView.cs");

            Assert.Contains("<views:GrillView x:Name=\"GrillViewControl\"", mainWindowXaml);
            Assert.DoesNotContain("<DataGrid x:Name=\"PilotBoard\"", mainWindowXaml);
            Assert.DoesNotContain("x:Name=\"BoardOverlayHost\"", mainWindowXaml);
            Assert.DoesNotContain("x:Name=\"BoardModeHintOverlay\"", mainWindowXaml);

            // Pilot Detail remains a separate later extraction (PMG-24); PMG-23 must not absorb it.
            Assert.Contains("x:Name=\"DetailPane\"", mainWindowXaml);
            Assert.Contains("x:Name=\"KnownCynoOverrideCheckBox\"", mainWindowXaml);
            Assert.Contains("x:Name=\"NotesTagsBox\"", mainWindowXaml);
            Assert.DoesNotContain("x:Name=\"DetailPane\"", grillXaml);

            Assert.Contains("AutomationProperties.AutomationId=\"BoardOverlayHost\"", grillXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"PilotBoard\"", grillXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"BoardModeHintOverlay\"", grillXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"SigColumn\"", grillXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"CharacterColumn\"", grillXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"CynoHullSeenColumn\"", grillXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"PilotNoteButton\"", grillXaml);
            Assert.Contains("AutomationProperties.AutomationId=\"WatchedPilotMarker\"", grillXaml);

            Assert.Contains("GrillViewControl.PilotBoardControl", compatibilitySource);
            Assert.Contains("GrillViewControl.BoardOverlayHostGrid", compatibilitySource);
            Assert.Contains("GrillViewControl.CharacterColumnControl", compatibilitySource);
            Assert.Contains("GrillViewControl.CynoHullSeenColumnControl", compatibilitySource);
        }

        [Fact]
        public void AnalysisView_InheritedWindowResourcesUseDeferredLookup()
        {
            var analysisXaml = ReadRepoFile("PitmastersGrill", "Views", "AnalysisView.xaml");

            Assert.Contains("Style=\"{DynamicResource SettingsLabelStyle}\"", analysisXaml);
            Assert.DoesNotContain("Style=\"{StaticResource SettingsLabelStyle}\"", analysisXaml);
        }

        [Fact]
        public void GrillView_InheritedWindowResourcesUseDeferredLookup()
        {
            var grillXaml = ReadRepoFile("PitmastersGrill", "Views", "GrillView.xaml");

            Assert.Contains("ColumnHeaderStyle=\"{DynamicResource PilotBoardColumnHeaderStyle}\"", grillXaml);
            Assert.Contains("CellStyle=\"{DynamicResource PilotBoardCellStyle}\"", grillXaml);
            Assert.Contains("Style=\"{DynamicResource PilotNoteButtonStyle}\"", grillXaml);
            Assert.DoesNotContain("{StaticResource PilotBoardColumnHeaderStyle}", grillXaml);
            Assert.DoesNotContain("{StaticResource PilotBoardCellStyle}", grillXaml);
            Assert.DoesNotContain("{StaticResource PilotNoteButtonStyle}", grillXaml);
        }

        [Fact]
        public void AnalysisView_XamlLoadsWithoutParentResourceScope()
        {
            Exception? loadFailure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    _ = new AnalysisView();
                }
                catch (Exception ex)
                {
                    loadFailure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(loadFailure);
        }

        [Fact]
        public void GrillView_XamlLoadsWithoutParentResourceScope()
        {
            Exception? loadFailure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    _ = new GrillView();
                }
                catch (Exception ex)
                {
                    loadFailure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(loadFailure);
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

        [Fact]
        public void GrillViewCodeBehind_RemainsPresentationOnly()
        {
            var source = ReadRepoFile("PitmastersGrill", "Views", "GrillView.xaml.cs");

            Assert.Contains("PilotBoardControl", source);
            Assert.Contains("PilotNoteClick", source);
            Assert.Contains("BoardPreviewMouseRightButtonUp", source);
            Assert.DoesNotContain("Repository", source);
            Assert.DoesNotContain("Service", source);
            Assert.DoesNotContain("Persistence", source);
            Assert.DoesNotContain("AppSettings", source);
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

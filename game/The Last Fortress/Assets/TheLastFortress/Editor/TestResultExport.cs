using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Saves NUnit XML for every test run started in this Editor (menu, Test Runner window, or MCP) to
    /// TestResults/{EditMode|PlayMode}-{timestamp}.xml. Lets a live Editor produce the same evidence as
    /// the batch-mode -testResults command when the project is already open.
    /// </summary>
    [InitializeOnLoad]
    static class TestResultExport
    {
        const string Directory = "TestResults";

        static TestResultExport()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    System.IO.Directory.CreateDirectory(Directory);
                    var mode = result.Test.TestMode.HasFlag(TestMode.PlayMode) ? "PlayMode" : "EditMode";
                    var path = Path.Combine(Directory, $"{mode}-{DateTime.Now:yyyyMMdd-HHmmss}.xml");
                    TestRunnerApi.SaveResultToFile(result, path);
                    Debug.Log($"Test results saved to {Path.GetFullPath(path)} ({result.PassCount} passed, {result.FailCount} failed)");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Could not export test results: " + e.Message);
                }
            }

            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}

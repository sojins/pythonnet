using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using NUnit.Framework;

using Python.Runtime;
using Python.Test;

namespace Python.PythonTestsRunner
{
    public class PythonTestRunner
    {
        private void AddEnvPath(params string[] paths)
        {
            // PC에 설정되어 있는 환경 변수를 가져온다.
            var envPaths = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator).ToList();
            // 중복 환경 변수가 없으면 list에 넣는다.
            envPaths.InsertRange(0, paths.Where(x => x.Length > 0 && !envPaths.Contains(x)).ToArray());
            // 환경 변수를 다시 설정한다.
            Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator.ToString(), envPaths), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", "python38.dll");
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            string python_dir = @"D:\Utility\Python38";
            var PYTHON_HOME = Environment.ExpandEnvironmentVariables(python_dir);

            // 환경 변수 설정
            AddEnvPath(PYTHON_HOME);

            PythonEngine.PythonHome = PYTHON_HOME;
            Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", "python38.dll");
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        /// <summary>
        /// Selects the Python tests to be run as embedded tests.
        /// </summary>
        /// <returns></returns>
        static IEnumerable<string[]> PythonTestCases()
        {
            // Add the test that you want to debug here.
            yield return new[] { "test_indexer", "test_boolean_indexer" };
            yield return new[] { "test_delegate", "test_bool_delegate" };
            yield return new[] { "test_subclass", "test_implement_interface_and_class" };
        }

        /// <summary>
        /// Runs a test in src/tests/*.py as an embedded test.  This facilitates debugging.
        /// </summary>
        /// <param name="testFile">The file name without extension</param>
        /// <param name="testName">The name of the test method</param>
        [TestCaseSource(nameof(PythonTestCases))]
        public void RunPythonTest(string testFile, string testName)
        {
            // Find the tests directory
            string folder = typeof(PythonTestRunner).Assembly.Location;
            while (Path.GetFileName(folder) != "src")
            {
                folder = Path.GetDirectoryName(folder);
            }
            folder = Path.Combine(folder, "..", "tests");
            string path = Path.Combine(folder, testFile + ".py");
            if (!File.Exists(path)) throw new FileNotFoundException("Cannot find test file", path);

            // We could use 'import' below, but importlib gives more helpful error messages than 'import'
            // https://docs.python.org/3/library/importlib.html#importing-a-source-file-directly
            // Because the Python tests sometimes have relative imports, the module name must be inside the tests package
            PythonEngine.Exec($@"
import sys
import os
sys.path.append(os.path.dirname(r'{folder}'))
sys.path.append(os.path.join(r'{folder}', 'fixtures'))
import clr
clr.AddReference('Python.Test')
import tests
module_name = 'tests.{testFile}'
file_path = r'{path}'
import importlib.util
spec = importlib.util.spec_from_file_location(module_name, file_path)
module = importlib.util.module_from_spec(spec)
sys.modules[module_name] = module
try:
  spec.loader.exec_module(module)
except ImportError as error:
  raise ImportError(str(error) + ' when sys.path=' + os.pathsep.join(sys.path))
module.{testName}()
");
        }
    }
}

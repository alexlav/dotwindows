// build this with "csc /r:Microsoft.VisualBasic.dll /r:ZetaLongPaths.dll /t:exe /out:myke.exe /debug+ myke*.cs"

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ZetaLongPaths;

[Connector(name = "kep", priority = 999, description =
  "Wraps the development workflow of project Kepler.")]

public class Kep : Git {
  public override String project { get { return @"%PROJECTS%\Kepler".Expand(); } }
  //public virtual String profile { get { return "fastlocker"; } }
  public virtual String profile { get { return "locker.unlock locker.done"; } }

  public override bool accept() {
    if (Config.verbose) println("project = {0}, dir = {1}", project.Expand(), dir.FullName);
    return dir.IsChildOrEquivalentTo(project);
  }

  protected Arguments arguments;
  public Kep() : base() { init(); }
  public Kep(FileInfo file, Arguments arguments) : base(file) { init(); this.arguments = arguments; }
  public Kep(DirectoryInfo dir, Arguments arguments) : base(dir) { init(); this.arguments = arguments; }
  private void init() { env["ResultFileRegex"] = "([:.a-z_A-Z0-9\\\\/-]+[.]scala):([0-9]+)"; }

  public virtual bool inPlayground { get {
    return dir.FullName.Replace("\\", "/").Contains("/sandbox");
  } }

  public virtual bool inTest { get {
    if (file != null && file.FullName.Replace("\\", "/").Contains("/test/")) return true;

    var s = Path.GetFullPath(dir.FullName).Replace("\\", "/");
    var iof = s.IndexOf("/test/");
    if (iof == -1) return false;
    s = s.Substring(iof + "/test/".Length);
    // test/files/run will fail, but test/files/run/blah will pass
    return s.Where(c => c == '/').Count() > 1;
  } }

  public virtual bool inTestRoot { get {
    if (file != null && file.FullName.Replace("\\", "/").Contains("/test/")) return true;

    var s = Path.GetFullPath(dir.FullName).Replace("\\", "/");
    var iof = s.IndexOf("/test/");
    if (iof == -1) return false;
    s = s.Substring(iof + "/test/".Length);
    return s.Where(c => c == '/').Count() == 1;
  } }

  [Action]
  public virtual ExitCode clean() {
    if (inPlayground || inTest || inTestRoot) {
      dir.GetDirectories("*.obj").ToList().ForEach(dir1 => dir1.Delete(true));
      dir.GetFiles("*.class").ToList().ForEach(file1 => file1.Delete());
      dir.GetFiles("*.log").ToList().ForEach(file1 => file1.Delete());
      return 0;
    } else {
      //return Console.batch("ant all.clean -buildfile build.xml", home: root);
      println("error: clean for kepler is disabled to prevent occasional loss of work");
      return -1;
    }
  }

  [Action]
  public virtual ExitCode rebuild() {
    if (inPlayground || inTest) {
      var scala = file != null ? new Scala(file, arguments): new Scala(dir, arguments);
      return scala.rebuild();
    } else {
      return Console.batch("ant all.clean " + profile + " -buildfile build.xml", home: root);
    }
  }

  [Action]
  public virtual ExitCode rebuildLibrary() {
    return Console.batch("ant all.clean " + profile + " -buildfile build.xml", home: root);
  }

  [Action]
  public virtual ExitCode rebuildCompiler() {
    var compilerClasses = new DirectoryInfo(project + "\\build\\locker\\classes\\compiler");
    if (compilerClasses.Exists) ZlpIOHelper.DeleteDirectory(compilerClasses.FullName, true);
    var compilerToken = new FileInfo(project + "\\build\\locker\\compiler.complete");
    if (compilerToken.Exists) compilerToken.Delete();
    return Console.batch("ant " + profile + " -buildfile build.xml", home: root);
  }

  [Action]
  public virtual ExitCode rebuildAlt() {
    return rebuildAltLibrary();
  }

  [Action]
  public virtual ExitCode rebuildAltLibrary() {
    return Console.batch("ant clean build -buildfile build.xml", home: root);
  }

  [Action]
  public virtual ExitCode rebuildAltCompiler() {
    var compilerClasses = new DirectoryInfo(project + "\\build\\quick\\classes\\compiler");
    if (compilerClasses.Exists) ZlpIOHelper.DeleteDirectory(compilerClasses.FullName, true);
    var compilerToken = new FileInfo(project + "\\build\\quick\\compiler.complete");
    if (compilerToken.Exists) compilerToken.Delete();
    return Console.batch("ant build -buildfile build.xml", home: root);
  }

  [Default, Action]
  public virtual ExitCode compile() {
    if (inPlayground || inTest) {
      var scala = file != null ? new Scala(file, arguments): new Scala(dir, arguments);
      return scala.compile();
    } else {
      var status = Console.batch("ant " + profile + " -buildfile build.xml", home: root);
      if (!status) return status;

      var partest = project + @"\build\locker\classes\partest";
      if (!Directory.Exists(partest)) {
        var donor = new Donor(new DirectoryInfo(new Donor().project), arguments);
        return donor.compile();
      } else {
        return 0;
      }
    }
  }

  [Action]
  public virtual ExitCode compileAlt() {
    if (inPlayground || inTest) {
      var scala = file != null ? new Scala(file, arguments): new Scala(dir, arguments);
      return scala.compile();
    } else {
      var status = Console.batch("ant build -buildfile build.xml", home: root);
      if (!status) return status;

      var partest = project + @"\build\locker\classes\partest";
      if (!Directory.Exists(partest)) {
        var donor = new Donor(new DirectoryInfo(new Donor().project), arguments);
        return donor.compile();
      } else {
        return 0;
      }
    }
  }

  [Action]
  public virtual ExitCode repl() {
//    return compile() && println() && Console.interactive(Config.sublime ? "scala /sublime" : "scala", home: root);
    return Console.interactive(Config.sublime ? "scala /sublime" : "scala", home: root);
  }

  [Action]
  public virtual ExitCode run() {
    if (inPlayground || inTest) {
      var scala = file != null ? new Scala(file, arguments): new Scala(dir, arguments);
      return scala.run();
    } else {
      //var options = new List<String>();
      //options.Add("-deprecation");
      //options.Add("-Yreify-copypaste");
      //Func<String> readArguments = () => Console.readln(prompt: "Lift", history: String.Format("lift {0}", root.FullName));
      //options.Add("-e \"scala.reflect.mirror.reify{" + (arguments.Count > 0 ? arguments.ToString() : readArguments()) + "}\"");
      //return Console.batch("scala " + String.Join(" ", options.ToArray()));

/*
      var root = new DirectoryInfo(@"%PROJECTS%\Kepler\sandbox\".Expand());
      var files = root.GetFiles("*.scala", SearchOption.AllDirectories).ToList();
      if (files.Count == 0) {
        println("error: nothing to run");
        return -1;
      } else if (files.Count == 1) {
        var toRun = files[0];
        println("running {0}", toRun.FullName);
        var scala = new Scala(toRun, arguments);
        return scala.run();
      } else {
        println("error: command is ambiguous");
        files.Take(5).ToList().ForEach(file1 => println("    " + file1));
        if (files.Count > 5) println("    ... " + (files.Count - 5) + " more");
        return -1;
      }
*/
      return Console.batch("partest files\\pos\\t1693.scala", home: root);
    }
  }

  [Action]
  public override ExitCode runTest() {
    var prefix = project;
    prefix = prefix.Replace("/", "\\");
    if (!prefix.EndsWith("\\")) prefix += "\\";
    prefix += "test\\";

    List<String> tests;
    if (inTest) {
      var files = new List<FileSystemInfo>{(FileSystemInfo)file ?? dir}.Concat(arguments.Select(argument => new DirectoryInfo(argument).Exists ? (FileSystemInfo)new DirectoryInfo(argument) : new FileInfo(argument))).ToList();
      files = files.Where(file1 => file1.Exists).ToList();
      tests = files.Select(f => f.FullName.Substring(prefix.Length)).ToList();
    } else {
      var suite = getCurrentTestSuite();
      if (suite == null) { println("there is no test suite associated with this project"); return -1; }
      tests = getTestSuiteAllTests(suite) ?? new List<String>();
      tests.ForEach(test => { if (!test.StartsWith(prefix, true, CultureInfo.CurrentCulture)) throw new Exception("bad test: " + test); });
      tests = tests.Select(test => test.Substring(prefix.Length)).ToList();
    }

    tests = tests.Select(test => {
      var full = prefix + test;

      if (full.Contains("nyi") || (Directory.Exists(full) && Directory.GetFiles(full, "*nyi*").Count() > 0))
        return null;

      var suffixes = new []{ ".check", ".flags", "-run.log", "-neg.log", "-pos.log" };
      foreach (var suffix in suffixes) {
        if (full.EndsWith(suffix)) {
          var trimmed = full.Substring(0, full.Length - suffix.Length);
          if (Directory.Exists(trimmed)) {
            return trimmed.Substring(prefix.Length);
          }
          trimmed += ".scala";
          if (File.Exists(trimmed)) {
            return trimmed.Substring(prefix.Length);
          }
        }
      }

      if (full.EndsWith(".scala")) {
        return test;
      } else if (Directory.Exists(full)) {
        return test;
      } else {
        return null;
      }
    }).Where(test => test != null).Distinct().ToList();

    if (tests.Count == 0) { println("nothing to test!"); return -1; }
    traceln("[myke] testing: {0}", String.Join(" ", tests.ToArray()));
    var partest = @"%SCRIPTS_HOME%\partest.exe";
    return Console.batch("\"\"" + partest + "\"\" " + String.Join(" ", tests.ToArray()), home: root + "\\test");
  }

  [Action]
  public ExitCode runAllTests() {
    var status = Console.batch("ant build -buildfile build.xml", home: root);
    if (status) {
      var tests = calculateTestSuiteTests("all").Select(test => test.Substring((project + "\\test\\").Length)).ToList();
      traceln("[myke] testing: {0}", String.Join(" ", tests.ToArray()));
      status = Console.batch("ant test", home: root);
    }
    return status;
  }

  public override List<String> calculateTestSuiteTests(String profile) {
    Func<String, String, Func<String>, bool> filter = null;
    if (profile == "macro") {
      filter = (fullName, shortName, text0) => {
        var text = text0();
        var pos = fullName.Contains("macro") || text.Contains("macro");
        var neg = shortName == "test\\files\\run\\reify_printf.scala";
        return pos && !neg;
      };
    } else if (profile == "reify") {
      filter = (fullName, shortName, text0) => {
        var text = text0();
        var pos = fullName.Contains("reify") || text.Contains("reify") || text.Contains("Manifest") || text.Contains("ClassTag") || text.Contains("TypeTag") || text.Contains("GroundTypeTag");
        var neg = shortName.StartsWith("test\\files\\run\\macro-def-path-dependent");
        return pos && !neg;
      };
    } else if (profile == "array") {
      filter = (fullName, shortName, text0) => {
        var text = text0();
        var pos = text.Contains("Array") || text.Contains("ClassTag") || text.Contains("ClassManifest");
        var neg = false;
        return pos && !neg;
      };
    } else if (profile == "all") {
      filter = (fullName, shortName, text0) => true;
    }

    if (filter == null)
      return null;

    var ktr = new DirectoryInfo(root + "\\test\\files\\run");
    var ktp = new DirectoryInfo(root + "\\test\\files\\pos");
    var ktn = new DirectoryInfo(root + "\\test\\files\\neg");
    var ktj = new DirectoryInfo(root + "\\test\\files\\jvm");
    var ktdirs = new []{ktr, ktp, ktn, ktj}.ToList();
    return ktdirs.SelectMany(ktdir => ktdir.GetFiles("*.scala", SearchOption.AllDirectories).Concat(ktdir.GetFiles("*.java", SearchOption.AllDirectories)).Select(f => {
      var fullName = f.FullName;
      var prefix = ktdir.FullName;
      if (!prefix.EndsWith("\\")) prefix += "\\";
      var stripped = f.FullName.Substring(prefix.Length);
      var iof = stripped.IndexOf("\\");
      stripped = iof == -1 ? stripped : stripped.Substring(0, iof);
      var category = ktdir.FullName.Substring(root.FullName.Length);
      if (category.StartsWith("\\")) category = category.Substring(1);
      if (category.EndsWith("\\")) category = category.Substring(0, category.Length - 1);
      var shortName = category + "\\" + stripped;
      Func<String> text = () => File.ReadAllText(f.FullName);
      return filter(fullName, shortName, text) ? (project + "\\" + shortName) : null;
    })).Where(test => test != null).Distinct().ToList();
  }

  public override List<String> getTestSuiteFailedTests(String profile) {
    return getTestSuiteTestsInternal(profile, "failed");
  }
  public override List<String> getTestSuiteSucceededTests(String profile) {
    return getTestSuiteTestsInternal(profile, "succeeded");
  }

  public List<String> getTestSuiteTestsInternal(String profile, String kind) {
    var result = new List<String>();
    var traceDir = new DirectoryInfo(@"%HOME%\.myke".Expand());
    if (traceDir.Exists) {
      var logs = traceDir.GetFiles("*.log").OrderByDescending(fi => fi.LastWriteTime).ToList();
      var tests = null as List<String>;
      var relevant = logs.Where(log => {
        var lines = File.ReadAllLines(log.FullName);
        var shebang = lines.FirstOrDefault();
        if (shebang != null) {
          var psiPrefix = "[myke] testing: ";
          var psi = lines.FirstOrDefault(line => line.StartsWith(psiPrefix));
          if (psi != null) {
            tests = psi.Substring(psiPrefix.Length).Split(new[]{" "}, StringSplitOptions.None).Select(shortName => project + "\\test\\" + shortName).ToList();
            return true;
          }
        }
        return false;
      }).FirstOrDefault();
      if (relevant != null) {
        println(relevant.FullName);
        var fragments = File.ReadAllLines(relevant.FullName).Select(line => {
          var rxSucceeded = @"testing: \[\.\.\.\](?<filename>.*?)\s*\[  OK  \]$";
          var rxFailed = @"(testing: \[\.\.\.\](?<filename>.*?)\s*\[FAILED\]$)|(Possible compiler crash during test of: (?<filename>.*?)$)";
          var rx = kind == "failed" ? rxFailed : rxSucceeded;
          var m = Regex.Match(line, rx);
          return m.Success ? m.Result("${filename}") : null;
        }).Where(fragment => fragment != null).Distinct().ToList();
        fragments.ForEach(fragment => {
          var neg = fragment.Contains("\\scalap\\");
          neg = neg || fragment.Contains("\\scalacheck\\");
          neg = neg || fragment.Contains("\\buildmanager\\");
          neg = neg || fragment.Contains("\\res\\");
          if (!neg) {
            var matches = tests.Where(test => test.EndsWith(fragment)).ToList();
            if (matches.Count() != 1) result.Add(fragment);
            else {
              var match = matches[0];
              var flavor = match.Substring((project + "\\test\\files\\").Length);
              var iof = flavor.IndexOf("\\");
              flavor = flavor.Substring(0, iof);
              result.Add(match);
              var filename = Path.GetDirectoryName(match) + "\\" + Path.GetFileNameWithoutExtension(match);
              result.Add(filename + "-" + flavor + ".log");
              result.Add(filename + ".check");
            }
          }
        });
      }
    }
    result = result.OrderBy(name => name).ToList();
    return result;
  }

  private ExitCode transplantFile(String from, String to) {
    from = project + "\\" + from.Replace("/", "\\");
    to = project + "\\" + to.Replace("/", "\\");
    print("  * Copying {0} to {1}... ", from, to);

    try {
      ExitCode status = -1;
      if (File.Exists(from)) status = CopyFile(from, to);
      if (status) println("[  OK  ]");
      return status;
    } catch (Exception ex) {
      println("[FAILED]");
      println(ex);
      return -1;
    }
  }

  private static ExitCode CopyFile(string sourceFile, string destFile) {
    try {
      File.Copy(sourceFile, destFile, true);
      return 0;
    } catch (Exception ex) {
      println("[FAILED]");
      println(ex);
      return -1;
    }
  }

  [Action]
  public virtual ExitCode deploy() {
    var paloLibraryJar = new FileInfo(root + @"\build\palo\lib\scala-library.jar");
    var paloCompilerJar = new FileInfo(root + @"\build\palo\lib\scala-compiler.jar");
    var packLibraryJar = new FileInfo(root + @"\build\pack\lib\scala-library.jar");
    var packCompilerJar = new FileInfo(root + @"\build\pack\lib\scala-compiler.jar");
    var paloModTime = paloLibraryJar.Exists && paloCompilerJar.Exists ? (paloLibraryJar.LastWriteTime > paloCompilerJar.LastWriteTime ? paloLibraryJar.LastWriteTime : paloCompilerJar.LastWriteTime) : DateTime.MinValue;
    var packModTime = packLibraryJar.Exists && packCompilerJar.Exists ? (packLibraryJar.LastWriteTime > packCompilerJar.LastWriteTime ? packLibraryJar.LastWriteTime : packCompilerJar.LastWriteTime) : DateTime.MinValue;
    var source = paloModTime > packModTime ? "build/palo/" : "build/pack/";

    var status = (paloModTime != DateTime.MinValue || packModTime != DateTime.MinValue) ? (ExitCode)0 : (ExitCode)(-1);
    if (!status) {
      println("Couldn't find neither pack nor palo to deploy as a starr");
      return status;
    }

    println("Deploying starr upon ourselves...");
    status = status && transplantFile(source + "lib/scala-library.jar", "lib/scala-library.jar");
    status = status && transplantFile(source + "lib/scala-compiler.jar", "lib/scala-compiler.jar");
    return status;
  }
}
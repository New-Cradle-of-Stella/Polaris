using System.Linq;
using System.Threading;
using Polaris.Event.Compiler;
using Polaris.Event.Compiler.Text;
using Xunit;

namespace Polaris.Event.Compiler.Tests
{
    public class CompilerTests
    {
        const string AliasYaml = @"
version: 2.0
target: 0.29j
settings:
  textMode: heredoc
  defaultPosition: center
actors:
  Noel:
    raw: n
    defaultPos: left
    box: wide
    poses:
      Happy: a_1/a22__F1__f1__m1__b1__u0
      Sad: a_1/a18__F1__f2__m1__b1__u0
positions:
  left:
    talker: L
    boxPos: L
    from: LT
  center:
    talker: C
    boxPos: C
    from: T
boxStyles:
  wide: WIDE
audio:
  sfx:
    Door: door_open_01
events:
  FarmRule: ___city_farm/_rule
";

        static HppCompileResult Compile(string hxx, string alias = AliasYaml, bool strictRaw = false)
        {
            var project = new HppProject
            {
                Namespace = "com.example.mymod",
                RootNamespace = "MyMod",
                Files = new[] { new SourceText("Sample.phxx", hxx) },
                AliasFile = new SourceText("polaris.events.yaml", alias),
                StrictRaw = strictRaw,
            };
            return new HppCompiler().Compile(project, CancellationToken.None);
        }

        [Fact]
        public void CharAndDialogue_LowersToTalkerPicHkdsMsg()
        {
            var result = Compile("@char Noel.Happy pos:left\nNoel: 今天也要加油。\n@return\n");

            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            var lines = result.Files.Single().CommandText.Split('\n');
            Assert.Contains("TALKER n L", lines);
            Assert.Contains("PIC n a_1/a22__F1__f1__m1__b1__u0", lines);
            Assert.Contains("HKDS n L LT WIDE", lines);
            Assert.Contains("今天也要加油。", lines);
        }

        [Fact]
        public void UnknownPose_ReportsHpp2103WithSuggestion()
        {
            var result = Compile("@char Noel.Hapy pos:left\n@return\n");

            Assert.False(result.Success);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("HPP2103", diagnostic.Code);
            Assert.Contains("Happy", diagnostic.Suggestion);
        }

        [Fact]
        public void Goto_ToUndefinedLabel_ReportsHpp3001()
        {
            var result = Compile("@goto #Missing\n");

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "HPP3001");
        }

        [Fact]
        public void Goto_ToDefinedLabel_LowersToLabelAndGoto()
        {
            var result = Compile("@goto #Leave\n# Leave\n@return\n");

            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            var lines = result.Files.Single().CommandText.Split('\n');
            Assert.Contains("GOTO Leave", lines);
            Assert.Contains("LABEL Leave", lines);
            Assert.Contains("SEEK_END", lines);
        }

        [Fact]
        public void DuplicateLabel_ReportsHpp3002()
        {
            var result = Compile("# Leave\n# Leave\n");

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "HPP3002");
        }

        [Fact]
        public void IfElse_LowersToIfElseBlocks()
        {
            var result = Compile("@if score >= 3\n    @sfx Door\n@else\n    @return\n@return\n");

            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            var lines = result.Files.Single().CommandText.Split('\n');
            Assert.Contains("IF 'score >= 3' {", lines);
            Assert.Contains("SND door_open_01", lines);
            Assert.Contains("ELSE {", lines);
            Assert.Contains("SEEK_END", lines);
        }

        [Fact]
        public void DanglingElse_ReportsHpp1004()
        {
            var result = Compile("@else\n");

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "HPP1004");
        }

        [Fact]
        public void Raw_PassesThroughVerbatim()
        {
            var result = Compile("@raw \"SMNCREATOR TARGET_SCRIPT demo\"\n@return\n");

            Assert.True(result.Success);
            var lines = result.Files.Single().CommandText.Split('\n');
            Assert.Contains("SMNCREATOR TARGET_SCRIPT demo", lines);
        }

        [Fact]
        public void Raw_InStrictMode_ReportsHpp9001Warning()
        {
            var result = Compile("@raw \"SMNCREATOR TARGET_SCRIPT demo\"\n@return\n", strictRaw: true);

            Assert.True(result.Success); // 警告不阻断编译
            Assert.Contains(result.Diagnostics, d => d.Code == "HPP9001");
        }

        [Fact]
        public void Call_ResolvesEventAliasAndArgs()
        {
            var result = Compile("@call FarmRule args:a,b\n@return\n");

            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            var lines = result.Files.Single().CommandText.Split('\n');
            Assert.Contains("CHANGE_EVENT2 ___city_farm/_rule a b", lines);
        }

        [Fact]
        public void Set_LowersPlainAndCompoundAssignment()
        {
            var result = Compile("@set score = 1\n@set score += 1\n@set score -= 1\n@return\n");

            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            var lines = result.Files.Single().CommandText.Split('\n');
            Assert.Contains("score=1", lines);
            Assert.Contains("score=~score+1", lines);
            Assert.Contains("score=~score-1", lines);
        }

        [Fact]
        public void Wait_RejectsNegativeFrames()
        {
            var result = Compile("@wait -5\n");

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "HPP3101");
        }

        [Fact]
        public void GeneratedCSharp_ContainsRegistrarAndReference()
        {
            var result = Compile("@return\n");

            Assert.True(result.Success);
            string cs = result.Files.Single().GeneratedCSharp;
            Assert.Contains("PolarisEventAutoRegistration(\"com.example.mymod\")", cs);
            Assert.Contains("class Sample_Registrar", cs);
            Assert.Contains("namespace MyMod.Generated.Events", cs);
        }

        [Fact]
        public void MissingTopLevelReturn_ReportsHpp3004()
        {
            var result = Compile("@sfx Door\n");

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "HPP3004");
        }

        [Fact]
        public void CodeAfterReturn_ReportsHpp3003ButStillCompiles()
        {
            var result = Compile("@return\n@sfx Door\n");

            Assert.True(result.Success, string.Join("\n", result.Diagnostics)); // 警告不阻断编译
            Assert.Contains(result.Diagnostics, d => d.Code == "HPP3003");
            var lines = result.Files.Single().CommandText.Split('\n');
            Assert.Contains("SEEK_END", lines);
            Assert.Contains("SND door_open_01", lines); // 不可达也照样降级输出，跟 C# 的 CS0162 一个态度
        }

        [Fact]
        public void ReturnInsideIfOnly_StillReportsMissingTopLevelReturn()
        {
            // 两支都 return 不代表顶层"必然"结束在这里——这条规则是刻意简化，不做穷尽性分支分析，
            // 详见 HxxLowerer.Lower 上的注释。
            var result = Compile("@if score >= 3\n    @return\n@else\n    @return\n");

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "HPP3004");
        }

        [Fact]
        public void MissingAliasFile_ReportsUnknownActorForEveryReference()
        {
            var result = Compile("@char Noel.Happy pos:left\n", alias: "version: 2.0\n");

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "HPP2101");
        }
    }
}

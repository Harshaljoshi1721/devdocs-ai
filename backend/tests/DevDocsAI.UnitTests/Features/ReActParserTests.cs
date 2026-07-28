using DevDocsAI.Application.Features.Agents;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class ReActParserTests
{
    [Fact]
    public void Parses_a_final_answer()
    {
        var step = ReActParser.Parse("""{"thought":"done","final_answer":"It uses JWT."}""");
        var final = step.ShouldBeOfType<FinalStep>();
        final.Answer.ShouldBe("It uses JWT.");
    }

    [Fact]
    public void Parses_an_action_with_arguments()
    {
        var step = ReActParser.Parse("""{"action":{"tool":"SearchProject","arguments":{"query":"auth"}}}""");
        var action = step.ShouldBeOfType<ActionStep>();
        action.Tool.ShouldBe("SearchProject");
        action.Arguments.GetProperty("query").GetString().ShouldBe("auth");
    }

    [Fact]
    public void Parses_action_when_arguments_are_omitted()
    {
        var step = ReActParser.Parse("""{"action":{"tool":"GetProjectStructure"}}""");
        var action = step.ShouldBeOfType<ActionStep>();
        action.Tool.ShouldBe("GetProjectStructure");
        action.Arguments.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public void Extracts_json_from_code_fences_and_surrounding_prose()
    {
        var raw = "Sure!\n```json\n{\"final_answer\":\"hi\"}\n```\nHope that helps.";
        ReActParser.Parse(raw).ShouldBeOfType<FinalStep>().Answer.ShouldBe("hi");
    }

    [Fact]
    public void Returns_unparseable_for_non_json_or_wrong_shape()
    {
        ReActParser.Parse("I don't know how to respond").ShouldBeOfType<UnparseableStep>();
        ReActParser.Parse("""{"something":"else"}""").ShouldBeOfType<UnparseableStep>();
    }
}

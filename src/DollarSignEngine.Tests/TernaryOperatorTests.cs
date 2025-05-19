using FluentAssertions;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class TernaryOperatorTests : TestBase
{
    public class TestItem
    {
        public bool Condition { get; set; }
        public string TrueValue { get; set; }
        public string FalseValue { get; set; }
    }

    public TernaryOperatorTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache(); // Clear cache before each test to ensure isolation
    }

    [Fact]
    public async Task Should_Evaluate_Ternary_Operator_True_Condition()
    {
        // Arrange
        var parameters = new TestItem { Condition = true, TrueValue = "Yes", FalseValue = "No" };
        var template = "{(Condition ? TrueValue : FalseValue)}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"{(parameters.Condition ? parameters.TrueValue : parameters.FalseValue)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Evaluate_Ternary_Operator_False_Condition()
    {
        // Arrange
        var parameters = new { condition = false, trueValue = "Yes", falseValue = "No" };
        var template = "{(condition ? trueValue : falseValue)}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"{(parameters.condition ? parameters.trueValue : parameters.falseValue)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Evaluate_Ternary_Operator_With_Literals_In_Expression()
    {
        // Arrange
        var templateTrue = "{(true ? \"Correct\" : \"Incorrect\")}";
        var templateFalse = "{(false ? \"Incorrect\" : \"Correct\")}";

        // Act
        var resultTrue = await DollarSign.EvalAsync(templateTrue); // No parameters needed
        var resultFalse = await DollarSign.EvalAsync(templateFalse); // No parameters needed

        // Assert
        var expectedTrue = $"{(true ? "Correct" : "Incorrect")}";
        var expectedFalse = $"{(false ? "Incorrect" : "Correct")}";
        resultTrue.Should().Be(expectedTrue);
        resultFalse.Should().Be(expectedFalse);
    }

    [Fact]
    public async Task Should_Evaluate_Nested_Ternary_Operator()
    {
        // Arrange
        var parameters = new { a = 10, b = 20 };
        var template = "{(a > 5 ? (b < 30 ? \"Both true\" : \"A true, B false\") : \"A false\")}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"{(parameters.a > 5 ? (parameters.b < 30 ? "Both true" : "A true, B false") : "A false")}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Evaluate_Ternary_Operator_With_Complex_Objects()
    {
        // Arrange
        var data = new
        {
            User = new { IsLoggedIn = true, Name = "John" },
            GuestName = "Guest"
        };
        var template = "{(User.IsLoggedIn ? User.Name : GuestName)}";

        // Act
        var result = await DollarSign.EvalAsync(template, data);

        // Assert
        var expected = $"{(data.User.IsLoggedIn ? data.User.Name : data.GuestName)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Evaluate_Ternary_Operator_With_Dictionary_Values()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["isActive"] = false,
            ["activeText"] = "User is Active",
            ["inactiveText"] = "User is Inactive"
        };
        // For dictionary access, the rewriter casts to original types.
        // If original type was object, it will be (object)Globals["..."],
        // so direct use in bool condition needs cast in expression.
        var template = "{((bool)isActive ? (string)activeText : (string)inactiveText)}";


        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"{(((bool)parameters["isActive"]) ? (string)parameters["activeText"] : (string)parameters["inactiveText"])}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Ternary_Operator_With_Format_Specifiers_In_Branches()
    {
        // Arrange
        var parameters = new { value = 123.456, useCurrency = true, date = new DateTime(2023, 10, 15) };

        // FIX: Cast branches to object to make the C# ternary expression valid
        // The DollarSign engine will then call ToString() on the resulting object.
        var template = "{(useCurrency ? (object)value : (object)date)}";
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert the raw string output from the first template
        string expectedRawOutput;
        if (parameters.useCurrency)
        {
            expectedRawOutput = parameters.value.ToString(); // Default double.ToString()
        }
        else
        {
            expectedRawOutput = parameters.date.ToString(); // Default DateTime.ToString()
        }
        result.Should().Be(expectedRawOutput);

        // This part of the test checks if formatted strings produced by the ternary are handled correctly.
        var templateProducingFormattedString = "{(useCurrency ? $\"{value:C2}\" : $\"{date:yyyy-MM-dd}\")}";
        var resultAdv = await DollarSign.EvalAsync(templateProducingFormattedString, parameters);
        var expectedAdv = $"{(parameters.useCurrency ? $"{parameters.value:C2}" : $"{parameters.date:yyyy-MM-dd}")}";
        resultAdv.Should().Be(expectedAdv);
    }


    [Fact]
    public async Task Should_Handle_Ternary_Operator_With_Escaped_Braces_In_Result_Strings()
    {
        // Arrange
        var parameters = new { condition = true, name = "Alice" };
        var template = "{(condition ? \"Hello, {{name}}\" : \"Goodbye, {name}\")}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        // If condition is true, expression is "Hello, {{name}}". DollarSign EvalAsync turns this into "Hello, {name}" (unescaping)
        // If condition is false, expression is "Goodbye, {name}". DollarSign EvalAsync interpolates name.
        var expected = parameters.condition ? "Hello, {name}" : $"Goodbye, {parameters.name}";
        result.Should().Be(expected);
    }


    [Fact]
    public async Task Should_Handle_Ternary_Operator_With_Interpolation_In_Result_Strings_Branches()
    {
        // Arrange
        var parameters = new { condition = false, name = "Bob", city = "New York" };
        var template = "{(condition ? $\"Welcome, {name}!\" : $\"Visit {city}, {name}.\")}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"{(parameters.condition ? $"Welcome, {parameters.name}!" : $"Visit {parameters.city}, {parameters.name}.")}";
        result.Should().Be(expected);
    }

    [Fact]
    public void Should_Handle_Synchronous_Ternary_Evaluation()
    {
        // Arrange
        var parameters = new { isAdmin = false, accessLevel = "User" };
        var template = "{(isAdmin ? \"Admin Access\" : $\"Access Level: {accessLevel}\")}";

        // Act
        var result = DollarSign.Eval(template, parameters);

        // Assert
        var expected = $"{(parameters.isAdmin ? "Admin Access" : $"Access Level: {parameters.accessLevel}")}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Ternary_With_Nullable_Boolean_Condition()
    {
        // Arrange
        var parameters = new { status = (bool?)null, trueMessage = "True", falseMessage = "False" };
        // The expression needs to handle null, e.g., (status == true ? ...) or (status ?? false ? ...)
        var template = "{((status == true) ? trueMessage : falseMessage)}"; // Handles null explicitly

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"{((parameters.status == true) ? parameters.trueMessage : parameters.falseMessage)}";
        result.Should().Be(expected);


        // Test with (status ?? false)
        var templateCoalesce = "{((status ?? false) ? trueMessage : falseMessage)}";
        var resultCoalesce = await DollarSign.EvalAsync(templateCoalesce, parameters);
        var expectedCoalesce = $"{((parameters.status ?? false) ? parameters.trueMessage : parameters.falseMessage)}";
        resultCoalesce.Should().Be(expectedCoalesce);
    }


    [Fact]
    public async Task Should_Handle_Ternary_With_Null_In_True_Branch_Variable_Resulting_In_Empty_String()
    {
        // Arrange
        var parameters = new { condition = true, valA = (string?)null, valB = "Not Null" };
        var template = "{(condition ? valA : valB)}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        // DollarSign.cs converts null script result to string.Empty.
        var expected = $"{(parameters.condition ? (parameters.valA ?? string.Empty) : parameters.valB)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Ternary_With_Null_In_False_Branch_Variable_Resulting_In_Empty_String()
    {
        // Arrange
        var parameters = new { condition = false, valA = "Not Null", valB = (string?)null };
        var template = "{(condition ? valA : valB)}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"{(parameters.condition ? parameters.valA : (parameters.valB ?? string.Empty))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Ternary_Operator_With_Arithmetic_In_Condition()
    {
        // Arrange
        var parameters = new { x = 5, y = 10 };
        var template = "{((x * 2) == y ? \"Equal\" : \"Not Equal\")}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"{(((parameters.x * 2) == parameters.y) ? "Equal" : "Not Equal")}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Ternary_Operator_With_String_Comparison_In_Condition()
    {
        // Arrange
        var parameters = new { name1 = "Test", name2 = "Test" };
        var template = "{(name1 == name2 ? \"Match\" : \"No Match\")}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"{(parameters.name1 == parameters.name2 ? "Match" : "No Match")}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Ternary_Operator_Within_Larger_String_Context()
    {
        // Arrange
        var parameters = new { isLoggedIn = true, userName = "Admin" };
        var template = "User: {userName}, Status: {(isLoggedIn ? \"Logged In\" : \"Logged Out\")}";

        // Act
        var result = await DollarSign.EvalAsync(template, parameters);

        // Assert
        var expected = $"User: {parameters.userName}, Status: {(parameters.isLoggedIn ? "Logged In" : "Logged Out")}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Ternary_Operator_With_Boolean_Literals_In_Branches()
    {
        // Arrange
        var parameters = new { check = true };
        // The result of the ternary will be a boolean, which .ToString() will convert to "True" or "False".
        var templateTrueOutcome = "{(check ? true : false)}";
        var templateFalseOutcome = "{(!check ? true : false)}";

        // Act
        var resultTrue = await DollarSign.EvalAsync(templateTrueOutcome, parameters);
        var resultFalse = await DollarSign.EvalAsync(templateFalseOutcome, parameters);

        // Assert
        // C# bool.ToString() is "True" or "False".
        var expectedTrue = (parameters.check ? true : false).ToString();
        var expectedFalse = (!parameters.check ? true : false).ToString();

        resultTrue.Should().Be(expectedTrue);
        resultFalse.Should().Be(expectedFalse);
    }
}
using FluentAssertions;
using Xunit.Abstractions;
using System.Globalization;

namespace DollarSignEngine.Tests;

public class FormatSpecifierTests : TestBase
{
    public FormatSpecifierTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task Should_Match_CSharp_NumericFormatting()
    {
        // Arrange
        var parameters = new
        {
            integer = 12345,
            vDecimal = 12345.6789,  // 변수명이 vDecimal
            money = 1234.56
        };

        // 다양한 숫자 형식 지정자
        var expression =
            "Default: {integer}, {vDecimal}\n" +  // 여기서는 decimal로 참조
            "Currency(C): {money:C}\n" +
            "Decimal(D10): {integer:D10}\n" +
            "Exponential(E): {vDecimal:E}\n" +    // 여기서도 decimal로 참조
            "Fixed-point(F2): {vDecimal:F2}\n" +  // 여기서도 decimal로 참조
            "General(G): {vDecimal:G}\n" +        // 여기서도 decimal로 참조
            "Number(N): {integer:N}\n" +
            "Percent(P): {vDecimal:P}\n" +        // 여기서도 decimal로 참조
            "Hexadecimal(X): {integer:X}";

        // Act
        var result = await DollarSign.EvalAsync(expression, parameters);

        // Assert
        var expected =
            $"Default: {parameters.integer}, {parameters.vDecimal}\n" +
            $"Currency(C): {parameters.money:C}\n" +
            $"Decimal(D10): {parameters.integer:D10}\n" +
            $"Exponential(E): {parameters.vDecimal:E}\n" +
            $"Fixed-point(F2): {parameters.vDecimal:F2}\n" +
            $"General(G): {parameters.vDecimal:G}\n" +
            $"Number(N): {parameters.integer:N}\n" +
            $"Percent(P): {parameters.vDecimal:P}\n" +
            $"Hexadecimal(X): {parameters.integer:X}";

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_DateTimeFormatting()
    {
        // Arrange
        var parameters = new
        {
            date = new DateTime(2023, 10, 15, 14, 30, 25)
        };

        // 다양한 날짜 형식 지정자
        var expression =
            "Default: {date}\n" +
            "Short Date(d): {date:d}\n" +
            "Long Date(D): {date:D}\n" +
            "Full(F): {date:F}\n" +
            "General(G): {date:G}\n" +
            "Month/Day(M): {date:M}\n" +
            "Sortable(s): {date:s}\n" +
            "Short Time(t): {date:t}\n" +
            "Long Time(T): {date:T}\n" +
            "Universal(U): {date:U}\n" +
            "Custom(yyyy-MM-dd): {date:yyyy-MM-dd}\n" +
            "Custom(HH:mm:ss): {date:HH:mm:ss}";

        // Act
        var result = await DollarSign.EvalAsync(expression, parameters);

        // Assert
        var expected =
            $"Default: {parameters.date}\n" +
            $"Short Date(d): {parameters.date:d}\n" +
            $"Long Date(D): {parameters.date:D}\n" +
            $"Full(F): {parameters.date:F}\n" +
            $"General(G): {parameters.date:G}\n" +
            $"Month/Day(M): {parameters.date:M}\n" +
            $"Sortable(s): {parameters.date:s}\n" +
            $"Short Time(t): {parameters.date:t}\n" +
            $"Long Time(T): {parameters.date:T}\n" +
            $"Universal(U): {parameters.date:U}\n" +
            $"Custom(yyyy-MM-dd): {parameters.date:yyyy-MM-dd}\n" +
            $"Custom(HH:mm:ss): {parameters.date:HH:mm:ss}";

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_EnumFormatting()
    {
        // Arrange
        var parameters = new
        {
            dayOfWeek = DayOfWeek.Wednesday,
            fileMode = FileMode.OpenOrCreate,
            stringComparison = StringComparison.OrdinalIgnoreCase
        };

        // 열거형 형식 지정자
        var expression =
            "Default: {dayOfWeek}, {fileMode}, {stringComparison}\n" +
            "General(G): {dayOfWeek:G}, {fileMode:G}\n" +
            "Decimal(D): {dayOfWeek:D}, {fileMode:D}\n" +
            "Hexadecimal(X): {dayOfWeek:X}, {fileMode:X}";

        // Act
        var result = await DollarSign.EvalAsync(expression, parameters);

        // Assert
        var expected =
            $"Default: {parameters.dayOfWeek}, {parameters.fileMode}, {parameters.stringComparison}\n" +
            $"General(G): {parameters.dayOfWeek:G}, {parameters.fileMode:G}\n" +
            $"Decimal(D): {parameters.dayOfWeek:D}, {parameters.fileMode:D}\n" +
            $"Hexadecimal(X): {parameters.dayOfWeek:X}, {parameters.fileMode:X}";

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_GuidFormatting()
    {
        // Arrange
        var parameters = new
        {
            guid = Guid.NewGuid()
        };

        // Guid 형식 지정자
        var expression =
            "Default: {guid}\n" +
            "No Hyphens(N): {guid:N}\n" +
            "Braces(B): {guid:B}\n" +
            "Parentheses(P): {guid:P}\n" +
            "Hyphens(D): {guid:D}";

        // Act
        var result = await DollarSign.EvalAsync(expression, parameters);

        // Assert
        var expected =
            $"Default: {parameters.guid}\n" +
            $"No Hyphens(N): {parameters.guid:N}\n" +
            $"Braces(B): {parameters.guid:B}\n" +
            $"Parentheses(P): {parameters.guid:P}\n" +
            $"Hyphens(D): {parameters.guid:D}";

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_AlignmentFormatting()
    {
        // Arrange
        var parameters = new
        {
            left = "Left",
            right = "Right",
            center = "Center"
        };

        // 정렬 형식 지정자
        var expression =
            "Left aligned(10): '|{left,-10}|'\n" +
            "Right aligned(10): '|{right,10}|'\n" +
            "Left aligned(5): '|{left,-5}|'\n" +
            "Right aligned(5): '|{right,5}|'\n" +
            "Default: '|{center}|'";

        // Assert
        var expected =
            $"Left aligned(10): '|{parameters.left,-10}|'\n" +
            $"Right aligned(10): '|{parameters.right,10}|'\n" +
            $"Left aligned(5): '|{parameters.left,-5}|'\n" +
            $"Right aligned(5): '|{parameters.right,5}|'\n" +
            $"Default: '|{parameters.center}|'";

        // Act
        var result = await DollarSign.EvalAsync(expression, parameters);


        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_CultureSpecificFormatting()
    {
        // Arrange
        var parameters = new
        {
            amount = 1234.56,
            date = new DateTime(2023, 10, 15)
        };

        // 현재 문화권 저장
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            // 미국 문화권 설정
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            // 미국 문화권 테스트
            var usResult = await DollarSign.EvalAsync(
                "Currency: {amount:C}, Date: {date:D}", parameters);
            var usExpected =
                $"Currency: {parameters.amount:C}, Date: {parameters.date:D}";

            usResult.Should().Be(usExpected);

            // 프랑스 문화권 설정
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            // 프랑스 문화권 테스트
            var frResult = await DollarSign.EvalAsync(
                "Currency: {amount:C}, Date: {date:D}", parameters);
            var frExpected =
                $"Currency: {parameters.amount:C}, Date: {parameters.date:D}";

            frResult.Should().Be(frExpected);

            // 일본 문화권 설정
            CultureInfo.CurrentCulture = new CultureInfo("ja-JP");

            // 일본 문화권 테스트
            var jpResult = await DollarSign.EvalAsync(
                "Currency: {amount:C}, Date: {date:D}", parameters);
            var jpExpected =
                $"Currency: {parameters.amount:C}, Date: {parameters.date:D}";

            jpResult.Should().Be(jpExpected);

            // 서로 다른 문화권 결과는 달라야 함
            usResult.Should().NotBe(frResult);
            frResult.Should().NotBe(jpResult);
            jpResult.Should().NotBe(usResult);
        }
        finally
        {
            // 원래 문화권 복원
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Should_Match_CSharp_CustomFormatting()
    {
        // Arrange
        var parameters = new
        {
            number = 42.123,
            hexNumber = 255,
            date = new DateTime(2023, 10, 15, 14, 30, 25),
            phoneNumber = 5551234567
        };

        // 사용자 지정 형식
        var expression =
            "Number with 4 decimals: {number:0.0000}\n" +
            "Percentage with 1 decimal: {number:0.0%}\n" +
            "Hex with prefix: {hexNumber:0x000000}\n" +
            "Custom Date: {date:yyyy년 MM월 dd일 HH시 mm분}\n" +
            "Phone format: {phoneNumber:(###) ###-####}";

        // Act
        var result = await DollarSign.EvalAsync(expression, parameters);

        // Assert
        var expected =
            $"Number with 4 decimals: {parameters.number:0.0000}\n" +
            $"Percentage with 1 decimal: {parameters.number:0.0%}\n" +
            $"Hex with prefix: {parameters.hexNumber:0x000000}\n" +
            $"Custom Date: {parameters.date:yyyy년 MM월 dd일 HH시 mm분}\n" +
            $"Phone format: {parameters.phoneNumber:(###) ###-####}";

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_ConditionalFormatting()
    {
        // Arrange
        var parameters = new
        {
            positiveValue = 42,
            negativeValue = -42,
            zeroValue = 0
        };

        // 조건부 형식화 (양수, 0, 음수)
        var expression =
            "Conditional positive: {positiveValue:#,#.00;(#,#.00);Zero}\n" +
            "Conditional negative: {negativeValue:#,#.00;(#,#.00);Zero}\n" +
            "Conditional zero: {zeroValue:#,#.00;(#,#.00);Zero}";

        // Act
        var result = await DollarSign.EvalAsync(expression, parameters);

        // Assert
        var expected =
            $"Conditional positive: {parameters.positiveValue:#,#.00;(#,#.00);Zero}\n" +
            $"Conditional negative: {parameters.negativeValue:#,#.00;(#,#.00);Zero}\n" +
            $"Conditional zero: {parameters.zeroValue:#,#.00;(#,#.00);Zero}";

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_ScientificNotation()
    {
        // Arrange
        var parameters = new
        {
            smallValue = 0.000000123,
            largeValue = 123000000000.0,
            normalValue = 123.456
        };

        // 지수 표기법
        var expression =
            "Small in scientific: {smallValue:E}\n" +
            "Large in scientific: {largeValue:E}\n" +
            "Normal in scientific: {normalValue:E}\n" +
            "With 2 decimals: {normalValue:E2}";

        // Act
        var result = await DollarSign.EvalAsync(expression, parameters);

        // Assert
        var expected =
            $"Small in scientific: {parameters.smallValue:E}\n" +
            $"Large in scientific: {parameters.largeValue:E}\n" +
            $"Normal in scientific: {parameters.normalValue:E}\n" +
            $"With 2 decimals: {parameters.normalValue:E2}";

        result.Should().Be(expected);
    }
}
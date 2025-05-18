using FluentAssertions;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests;

public class LinqTests : TestBase
{
    public LinqTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task Should_Support_Basic_Where_Clause()
    {
        // Arrange
        var parameters = new
        {
            numbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Even numbers: {string.Join(\", \", numbers.Where(n => n % 2 == 0))}", parameters);

        // Assert - Compare with C# LINQ
        var expected = $"Even numbers: {string.Join(", ", parameters.numbers.Where(n => n % 2 == 0))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Where_With_Select()
    {
        // Arrange
        var parameters = new
        {
            numbers = new[] { 1, 2, 3, 4, 5 }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Squared evens: {string.Join(\", \", numbers.Where(n => n % 2 == 0).Select(n => n * n))}", parameters);

        // Assert
        var expected = $"Squared evens: {string.Join(", ", parameters.numbers.Where(n => n % 2 == 0).Select(n => n * n))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Complex_Objects_With_Linq()
    {
        // Arrange
        var parameters = new
        {
            people = new[]
            {
                new { Name = "Alice", Age = 25 },
                new { Name = "Bob", Age = 30 },
                new { Name = "Charlie", Age = 20 },
                new { Name = "David", Age = 35 }
            }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "People over 25: {string.Join(\", \", people.Where(p => p.Age > 25).Select(p => p.Name))}", parameters);

        // Assert
        var expected = $"People over 25: {string.Join(", ", parameters.people.Where(p => p.Age > 25).Select(p => p.Name))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_OrderBy()
    {
        // Arrange
        var parameters = new
        {
            names = new[] { "Charlie", "Alice", "Bob", "David" }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Sorted names: {string.Join(\", \", names.OrderBy(n => n))}", parameters);

        // Assert
        var expected = $"Sorted names: {string.Join(", ", parameters.names.OrderBy(n => n))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_OrderByDescending()
    {
        // Arrange
        var parameters = new
        {
            numbers = new[] { 1, 5, 3, 9, 7 }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Descending numbers: {string.Join(\", \", numbers.OrderByDescending(n => n))}", parameters);

        // Assert
        var expected = $"Descending numbers: {string.Join(", ", parameters.numbers.OrderByDescending(n => n))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Skip_And_Take()
    {
        // Arrange
        var parameters = new
        {
            numbers = Enumerable.Range(1, 20).ToArray()
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Page 2 (items 6-10): {string.Join(\", \", numbers.Skip(5).Take(5))}", parameters);

        // Assert
        var expected = $"Page 2 (items 6-10): {string.Join(", ", parameters.numbers.Skip(5).Take(5))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_FirstOrDefault()
    {
        // Arrange
        var parameters = new
        {
            numbers = new[] { 1, 2, 3, 4, 5 }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "First even: {numbers.FirstOrDefault(n => n % 2 == 0)}, First > 10: {numbers.FirstOrDefault(n => n > 10)}", parameters);

        // Assert
        var expected = $"First even: {parameters.numbers.FirstOrDefault(n => n % 2 == 0)}, First > 10: {parameters.numbers.FirstOrDefault(n => n > 10)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_GroupBy()
    {
        // Arrange
        var parameters = new
        {
            words = new[] { "apple", "banana", "cherry", "avocado", "blueberry", "coconut" }
        };

        // Act
        var result = await DollarSign.EvalAsync(@"
            Grouped by first letter: 
            {string.Join("", "", words
                .GroupBy(w => w[0])
                .Select(g => $""{g.Key}: {string.Join("", "", g)}""))}", parameters);

        // Assert
        var expected = $@"
            Grouped by first letter: 
            {string.Join(", ", parameters.words
                .GroupBy(w => w[0])
                .Select(g => $"{g.Key}: {string.Join(", ", g)}"))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Any_And_All()
    {
        // Arrange
        var parameters = new
        {
            numbers = new[] { 2, 4, 6, 8, 10 }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "All even: {numbers.All(n => n % 2 == 0)}, Any > 5: {numbers.Any(n => n > 5)}", parameters);

        // Assert
        var expected = $"All even: {parameters.numbers.All(n => n % 2 == 0)}, Any > 5: {parameters.numbers.Any(n => n > 5)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Aggregate()
    {
        // Arrange
        var parameters = new
        {
            words = new[] { "Hello", "World", "of", "LINQ" }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Combined: {words.Aggregate((a, b) => a + \" \" + b)}", parameters);

        // Assert
        var expected = $"Combined: {parameters.words.Aggregate((a, b) => a + " " + b)}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Distinct()
    {
        // Arrange
        var parameters = new
        {
            numbers = new[] { 1, 2, 3, 2, 1, 4, 5, 4 }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Distinct numbers: {string.Join(\", \", numbers.Distinct())}", parameters);

        // Assert
        var expected = $"Distinct numbers: {string.Join(", ", parameters.numbers.Distinct())}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Count_And_Sum()
    {
        // Arrange
        var parameters = new
        {
            numbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Count: {numbers.Count()}, Even count: {numbers.Count(n => n % 2 == 0)}, Sum: {numbers.Sum()}", parameters);

        // Assert
        var expected = $"Count: {parameters.numbers.Count()}, Even count: {parameters.numbers.Count(n => n % 2 == 0)}, Sum: {parameters.numbers.Sum()}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Min_Max_Average()
    {
        // Arrange
        var parameters = new
        {
            numbers = new[] { 5, 10, 15, 20, 25 }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Min: {numbers.Min()}, Max: {numbers.Max()}, Average: {numbers.Average()}", parameters);

        // Assert
        var expected = $"Min: {parameters.numbers.Min()}, Max: {parameters.numbers.Max()}, Average: {parameters.numbers.Average()}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Nested_Complex_Linq_Queries()
    {
        // Arrange
        var parameters = new
        {
            products = new[]
            {
                new { Category = "Electronics", Name = "Laptop", Price = 1200.00 },
                new { Category = "Electronics", Name = "Phone", Price = 800.00 },
                new { Category = "Clothing", Name = "T-Shirt", Price = 20.00 },
                new { Category = "Clothing", Name = "Jeans", Price = 60.00 },
                new { Category = "Electronics", Name = "Headphones", Price = 150.00 },
                new { Category = "Books", Name = "Novel", Price = 15.00 }
            }
        };

        // Act
        var result = await DollarSign.EvalAsync(@"
            Categories by average price: 
            {string.Join("", "", products
                .GroupBy(p => p.Category)
                .Select(g => $""{g.Key}: ${g.Average(p => p.Price):F2}"")
                .OrderByDescending(s => double.Parse(s.Split(':')[1].Trim().Substring(1))))}", parameters);

        // Assert
        var expected = $@"
            Categories by average price: 
            {string.Join(", ", parameters.products
                .GroupBy(p => p.Category)
                .Select(g => $"{g.Key}: ${g.Average(p => p.Price):F2}")
                .OrderByDescending(s => double.Parse(s.Split(':')[1].Trim().Substring(1))))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_SelectMany()
    {
        // Arrange
        var parameters = new
        {
            families = new[]
            {
                new { FamilyName = "Smith", Members = new[] { "John", "Jane", "Jack" } },
                new { FamilyName = "Johnson", Members = new[] { "Robert", "Rita" } }
            }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "All members: {string.Join(\", \", families.SelectMany(f => f.Members))}", parameters);

        // Assert
        var expected = $"All members: {string.Join(", ", parameters.families.SelectMany(f => f.Members))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Zip()
    {
        // Arrange
        var parameters = new
        {
            names = new[] { "Alice", "Bob", "Charlie" },
            ages = new[] { 25, 30, 35 }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "People: {string.Join(\", \", names.Zip(ages, (name, age) => $\"{name} ({age})\"))}", parameters);

        // Assert
        var expected = $"People: {string.Join(", ", parameters.names.Zip(parameters.ages, (name, age) => $"{name} ({age})"))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Union_Intersect_Except()
    {
        // Arrange
        var parameters = new
        {
            set1 = new[] { 1, 2, 3, 4, 5 },
            set2 = new[] { 4, 5, 6, 7, 8 }
        };

        // Act
        var result = await DollarSign.EvalAsync(@"
            Union: {string.Join("", "", set1.Union(set2))}, 
            Intersect: {string.Join("", "", set1.Intersect(set2))}, 
            Except: {string.Join("", "", set1.Except(set2))}", parameters);

        // Assert
        var expected = $@"
            Union: {string.Join(", ", parameters.set1.Union(parameters.set2))}, 
            Intersect: {string.Join(", ", parameters.set1.Intersect(parameters.set2))}, 
            Except: {string.Join(", ", parameters.set1.Except(parameters.set2))}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Handle_Empty_Collections()
    {
        // Arrange
        var parameters = new
        {
            emptyList = new int[] { }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Count: {emptyList.Count()}, Any: {emptyList.Any()}, DefaultIfEmpty: {emptyList.DefaultIfEmpty().First()}", parameters);

        // Assert
        var expected = $"Count: {parameters.emptyList.Count()}, Any: {parameters.emptyList.Any()}, DefaultIfEmpty: {parameters.emptyList.DefaultIfEmpty().First()}";
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Support_Join_Operations()
    {
        // Arrange
        var parameters = new
        {
            customers = new[]
            {
                new { Id = 1, Name = "Alice" },
                new { Id = 2, Name = "Bob" },
                new { Id = 3, Name = "Charlie" }
            },
            orders = new[]
            {
                new { CustomerId = 1, OrderId = 101, Amount = 150.00 },
                new { CustomerId = 2, OrderId = 102, Amount = 200.00 },
                new { CustomerId = 1, OrderId = 103, Amount = 50.00 }
            }
        };

        // Act
        var result = await DollarSign.EvalAsync(@"
            Orders with customer names: 
            {string.Join("", "", customers.Join(
                orders, 
                customer => customer.Id, 
                order => order.CustomerId, 
                (customer, order) => $""{customer.Name}: Order #{order.OrderId} (${order.Amount:F2})""))}", parameters);

        // Assert
        var expected = $@"
            Orders with customer names: 
            {string.Join(", ", parameters.customers.Join(
                parameters.orders,
                customer => customer.Id,
                order => order.CustomerId,
                (customer, order) => $"{customer.Name}: Order #{order.OrderId} (${order.Amount:F2})"))}";
        result.Should().Be(expected);
    }
}
using Serious.Filters;
using Xunit;

public class QueryFilterTests
{
    public class TheApplyMethod
    {
        [Theory]
        [InlineData("name:Duck", null, new[] { "Daffy Duck", "Donald Duck", "Howard the Duck" })]
        [InlineData("Duck", "name", new[] { "Daffy Duck", "Donald Duck", "Howard the Duck" })]
        [InlineData("age:40", null, new[] { "Daffy Duck", "Donald Duck", "Tyrion" })]
        [InlineData("age:2 age:40", null, new[] { "Daffy Duck", "Donald Duck", "Tyrion" })]
        [InlineData("age:2", null, new string[] { })]
        [InlineData("age:40 age:2", null, new string[] { })]
        [InlineData("name:x", null, new string[] { })]
        [InlineData("age:40 name:Duck", null, new[] { "Daffy Duck", "Donald Duck" })]
        [InlineData("name:Daffy age:40 name:Duck", null, new[] { "Daffy Duck" })]
        [InlineData("name:o", null, new[] { "Donald Duck", "Howard the Duck", "Tyrion" })]
        [InlineData("blah blah", null, new[] { "Daffy Duck", "Donald Duck", "Howard the Duck", "Tyrion" })]
        [InlineData("blah blah", "name", new string[] { })]
        public void AppliesTheRelatedFilters(string searchText, string? defaultField, string[] expectedNames)
        {
            var filterList = FilterParser.Parse(searchText);
            var queryFilter = new QueryFilter<TestEntity>(
                new IFilterItemQuery<TestEntity>[]
                {
                    new NameFilter(),
                    new YoungerThanFilter(),
                });

            var entities = new[]
            {
                new TestEntity("Daffy Duck", 23),
                new TestEntity("Donald Duck", 21),
                new TestEntity("Howard the Duck", 48),
                new TestEntity("Tyrion", 16),
            };

            var query = queryFilter.Apply(entities.AsQueryable(), filterList, defaultField);
            var results = query.Select(r => r.Name).ToArray();
            Assert.Equal(expectedNames, results);
        }
    }

    record TestEntity(string Name, int Age);

    class NameFilter : IFilterItemQuery<TestEntity>
    {
        public string Field => "name";

        public IQueryable<TestEntity> Apply(IQueryable<TestEntity> query, Filter filter)
        {
            return query.Where(e => e.Name.ToLower().Contains(filter.LowerCaseValue, StringComparison.OrdinalIgnoreCase));
        }
    }

    class YoungerThanFilter : IFilterItemQuery<TestEntity>
    {
        public string Field => "age";

        public IQueryable<TestEntity> Apply(IQueryable<TestEntity> query, Filter filter)
        {
            return query.Where(e => e.Age < int.Parse(filter.Value));
        }

        public bool Exclusive => true;
    }
}

using Serious.Abbot;
using Serious.Abbot.Entities;
using Xunit;

namespace Abbot.Web.Tests.Infrastructure;

public class DomIdTests
{
    [Fact]
    public void GetDomIdForEntityBaseWorks()
    {
        var entity = new TestEntityWithSeveralWords()
        {
            Id = 42
        };

        Assert.Equal("test-entity-with-several-words-42", entity.GetDomId());
    }

    class TestEntityWithSeveralWords : EntityBase<TestEntityWithSeveralWords>
    {
    }
}

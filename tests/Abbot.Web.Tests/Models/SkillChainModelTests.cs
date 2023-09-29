using System;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Xunit;

public class SkillChainModelTests
{
    public class TheItemsProperty
    {
        [Fact]
        public void IsEmptyIfThereAreNoItems()
        {
            var currentIdentifier = Guid.Parse("d3e2804c-eabd-472a-b2b4-5c45b5b5f12c");
            var auditEvents = Enumerable.Empty<SkillRunAuditEvent>();
            var model = new SkillChainModel(currentIdentifier, auditEvents);

            Assert.Empty(model.Items);
        }

        [Fact]
        public void IsEmptyIfThereIsOnlyOneAuditEventInRelatedItems()
        {
            var currentIdentifier = Guid.Parse("d3e2804c-eabd-472a-b2b4-5c45b5b5f12c");
            var auditEvents = new[] { new SkillRunAuditEvent { Identifier = currentIdentifier } };
            var model = new SkillChainModel(currentIdentifier, auditEvents);

            Assert.Empty(model.Items);
        }

        [Fact]
        public void ReturnsChainInReverseOrder()
        {
            var rootGuid = Guid.Parse("00000001-eabd-472a-b2b4-5c45b5b5f12c");
            var secondGuid = Guid.Parse("00000002-eabd-472a-b2b4-5c45b5b5f12d");
            var thirdGuid = Guid.Parse("00000003-eabd-472a-b2b4-5c45b5b5f12d");
            var fourthGuid = Guid.Parse("00000004-eabd-472a-b2b4-5c45b5b5f12d");
            var auditEvents = new[]
            {
                new SkillRunAuditEvent { Identifier = rootGuid},
                new SkillRunAuditEvent { Identifier = thirdGuid, ParentIdentifier = secondGuid },
                new SkillRunAuditEvent { Identifier = secondGuid, ParentIdentifier = rootGuid },
                new SkillRunAuditEvent { Identifier = fourthGuid, ParentIdentifier = thirdGuid }
            };
            var model = new SkillChainModel(rootGuid, auditEvents);

            var items = model.Items.ToList();

            Assert.Equal(fourthGuid, items[0].Identifier);
            Assert.Equal(thirdGuid, items[1].Identifier);
            Assert.Equal(secondGuid, items[2].Identifier);
            Assert.Equal(rootGuid, items[3].Identifier);

        }
    }
}

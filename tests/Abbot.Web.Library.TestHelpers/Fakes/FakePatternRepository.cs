using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.TestHelpers
{
    public class FakePatternRepository : PatternRepository
    {
        public FakePatternRepository() : this(new FakeAbbotContext())
        {
        }

        public FakePatternRepository(FakeAbbotContext db) : base(db)
        {
            Db = db;
        }

        public FakeAbbotContext Db { get; }

        public async Task CreatePatternsAsync(params SkillPattern[] patterns)
        {
            await Db.SkillPatterns.AddRangeAsync(patterns);
            await Db.SaveChangesAsync();
        }
    }
}

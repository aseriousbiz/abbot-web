using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Xunit;

public class SkillEditPermissionModelTests
{
    public class TheGetCanEditSkillMethod
    {
        [Theory]
        [InlineData(/* protected */true, Capability.Edit,  /* expected */true)]
        [InlineData(/* protected */true, Capability.Admin, /* expected */true)]
        [InlineData(/* protected */true, Capability.None,  /* expected */false)]
        [InlineData(/* protected */false, Capability.None,  /* expected */true)]
        public void IsTrueUnderCorrectConditions(
            bool isProtected,
            Capability capability,
            bool expected)
        {
            var result = SkillEditPermissionModel.GetCanEditSkill(
                isProtected,
                capability);

            Assert.Equal(expected, result);
        }
    }

    public class TheGetCanEditCodeMethod
    {
        [Theory]
        [InlineData(/* protected */true, /*canEditSkill*/ true, /* force */ false, /* hasSourcePackage */false, /* expected */true)]
        [InlineData(/* protected */true, /*canEditSkill*/ true, /* force */ true,  /* hasSourcePackage */false, /* expected */true)]
        [InlineData(/* protected */true, /*canEditSkill*/ true, /* force */ true,  /* hasSourcePackage */true,  /* expected */true)]
        [InlineData(/* protected */true, /*canEditSkill*/ false,/* force */ true,  /* hasSourcePackage */false, /* expected */false)]
        [InlineData(/* protected */false,/*canEditSkill*/ true, /* force */ true,  /* hasSourcePackage */true,  /* expected */true)]
        [InlineData(/* protected */false,/*canEditSkill*/ false,/* force */ true,  /* hasSourcePackage */false, /* expected */false)]
        [InlineData(/* protected */false,/*canEditSkill*/ true, /* force */ false, /* hasSourcePackage */false, /* expected */true)]
        [InlineData(/* protected */false,/*canEditSkill*/ true, /* force */ true,  /* hasSourcePackage */false, /* expected */true)]
        public void IsTrueUnderCorrectConditions(
            bool isProtected,
            bool canEditSkill,
            bool forceEdit,
            bool hasSourcePackage,
            bool expected)
        {
            var result = SkillEditPermissionModel.GetCanEditCode(
                isProtected,
                canEditSkill,
                forceEdit,
                hasSourcePackage);

            Assert.Equal(expected, result);
        }
    }

    public class TheCanChangeRestrictedMethod
    {
        [Theory]
        [InlineData(false, Capability.Admin, /* expected */false)]
        [InlineData(true, Capability.Admin, /* expected */true)]
        [InlineData(true, Capability.Edit,  /* expected */false)]
        public void IsTrueUnderCorrectConditions(
            bool planAllowsPermissions,
            Capability capability,
            bool expected)
        {
            var model = new SkillEditPermissionModel(
                new Skill(),
                capability,
                false,
                planAllowsPermissions);

            var result = model.CanChangeRestricted;

            Assert.Equal(expected, result);
        }
    }


}

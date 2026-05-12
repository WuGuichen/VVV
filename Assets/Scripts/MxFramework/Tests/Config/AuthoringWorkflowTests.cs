using MxFramework.Config;
using MxFramework.Config.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class AuthoringWorkflowTests
    {
        [Test]
        public void BuffTemplate_CreatesPortablePlayerWorkflow()
        {
            AuthoringWorkflow workflow = BuffAuthoringWorkflowTemplate.CreateCreateBuffWorkflow(
                "buff.create.fire",
                "创建 Buff：燃烧",
                100001);

            Assert.AreEqual("Buff", workflow.Category);
            Assert.AreEqual(AuthoringWorkflowMode.Player, workflow.Mode);
            Assert.AreEqual("BuffFactoryData", workflow.Target.Source);
            Assert.AreEqual("Mod", workflow.Target.Layer);
            Assert.GreaterOrEqual(workflow.Steps.Count, 11);

            for (int i = 0; i < workflow.Steps.Count; i++)
            {
                Assert.IsFalse(workflow.Steps[i].RequiresUnity);
                Assert.IsFalse(workflow.Steps[i].RequiresSourceCode);
            }
        }

        [Test]
        public void BuffTemplate_TypeFieldsStep_HasTypeSpecificAction()
        {
            AuthoringWorkflow workflow = BuffAuthoringWorkflowTemplate.CreateCreateBuffWorkflow(
                "buff.create.fire",
                "创建 Buff：燃烧",
                100001);

            AuthoringWorkflowStep typeStep = workflow.GetStep("type-fields");

            Assert.NotNull(typeStep);
            Assert.AreEqual(AuthoringWorkflowStatus.Blocked, typeStep.Status);
            Assert.AreEqual(1, typeStep.QuickActions.Count);
            Assert.AreEqual(AuthoringQuickActionKind.OpenField, typeStep.QuickActions[0].Kind);
        }

        [Test]
        public void WorkflowStepAiContext_IsScopedToCurrentStep()
        {
            AuthoringWorkflow workflow = BuffAuthoringWorkflowTemplate.CreateCreateBuffWorkflow(
                "buff.create.fire",
                "创建 Buff：燃烧",
                100001);

            string context = workflow.CreateStepAiContext("common-fields");

            StringAssert.Contains("workflow=buff.create.fire", context);
            StringAssert.Contains("step=common-fields", context);
            StringAssert.Contains("targetSource=BuffFactoryData", context);
            StringAssert.Contains("BuffData.ID", context);
        }

        [Test]
        public void Workflow_GetStep_ReturnsRequestedStep()
        {
            AuthoringWorkflow workflow = BuffAuthoringWorkflowTemplate.CreateCreateBuffWorkflow(
                "buff.create.fire",
                "创建 Buff：燃烧",
                100001);

            AuthoringWorkflowStep step = workflow.GetStep("validation");

            Assert.NotNull(step);
            Assert.AreEqual("运行校验", step.Title);
        }
    }
}

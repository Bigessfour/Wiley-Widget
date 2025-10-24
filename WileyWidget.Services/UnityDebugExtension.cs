using System.Diagnostics;
using Unity.Builder;
using Unity.Strategies;

namespace WileyWidget.Services
{
    /// <summary>
    /// Unity container extension that logs resolution activity to help debug DI issues during development.
    /// Pattern follows Unity's custom extension guidance and plugs into the build pipeline at the type mapping stage.
    /// </summary>
    public sealed class UnityDebugExtension : Unity.Extension.UnityContainerExtension
    {
        protected override void Initialize()
        {
            Context.Strategies.Add(new DebugBuildStrategy(), UnityBuildStage.TypeMapping);
        }

        private sealed class DebugBuildStrategy : BuilderStrategy
        {
            public override void PreBuildUp(ref BuilderContext context)
            {
                var targetType = context.Type;
                Trace.WriteLine($"[Unity][PreBuild] Resolving {targetType?.FullName ?? "<null>"} (Name='{context.Name ?? string.Empty}')");
            }

            public override void PostBuildUp(ref BuilderContext context)
            {
                var targetType = context.Type;
                Trace.WriteLine($"[Unity][PostBuild] Resolved {targetType?.FullName ?? "<null>"} => {context.Existing?.GetType().FullName ?? "null"}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cthangover.Core.Utils;
using Cthangover.Mods.Rpg.Model;

namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>
    /// Visits effective variants and each independently applied state using the runtime resolver, without evaluating
    /// gameplay.
    /// </summary>
    public sealed class RpgObjectDataAudit
    {
        private readonly List<RpgDataIssue> issues = new();

        private readonly RpgValidationImages images;

        private readonly string projection;

        private readonly int patchSize;

        private readonly string source;

        private readonly string placement;

        private RpgObjectDataAudit(string projection, int patchSize, RpgValidationImages images, string source, string placement)
        {
            this.projection = projection;
            this.patchSize = patchSize;
            this.images = images;
            this.source = source;
            this.placement = placement;
        }

        /// <summary>
        /// Checks the active map variant and other declared variants with placement overrides, then each state's complete result.
        /// This is a bounded structural inventory, not a simulation of combinations of story conditions.
        /// </summary>
        public static IReadOnlyList<RpgDataIssue> Check(RpgObject value, string projection, int patchSize,
            RpgValidationImages images, string source, string mapVariant = null, RpgPlacement placement = null)
        {
            var audit = new RpgObjectDataAudit(projection, patchSize, images, source, placement?.ID);
            if (value == null)
            {
                return audit.issues;
            }

            var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mapVariant ?? "" };
            if (value.Variants != null)
            {
                variants.UnionWith(value.Variants.Keys);
            }

            foreach (var variant in variants)
            {
                var effective = RpgObjectResolve.Effective(value, variant.Length == 0 ? null : variant, placement);
                var context = "variant:" + (placement?.Variant ?? variant);
                audit.Visit(effective, context);
                for (var i = 0; i < (effective.States?.Count ?? 0); i++)
                {
                    audit.Visit(RpgObjectResolve.WithState(effective, i), context + "/state:" + i);
                }
            }

            return audit.issues;
        }

        private void Visit(RpgObject value, string context)
        {
            var node = (JsonObject)JsonSerializer.SerializeToNode(value, JsonOptionsProvider.ReadOptions);
            foreach (var issue in RpgDataCheck.Object(node, projection, patchSize, images.Size))
            {
                Attach(issue, value.ID, context);
            }

            if (value.Form == RpgObjectForm.Actor && !value.Hidden)
            {
                var detail = images.SheetProblem(value.Charset);
                if (detail != null)
                {
                    Attach(new RpgDataIssue
                    {
                        Code = "sheet",
                        Field = "Charset",
                        Detail = detail,
                    }, value.ID, context);
                }
            }
        }

        private void Attach(RpgDataIssue issue, string resource, string context)
        {
            issue.Resource = resource;
            issue.Context = context;
            issue.Source = source;
            issue.Placement = placement;
            issue.Projection = projection;
            issues.Add(issue);
        }
    }
}

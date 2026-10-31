namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Mods.Patches;

    /// <summary>
    /// One entry of an object's <c>States</c>: while <see cref="When"/> holds (and no later state
    /// does), the components it names replace the resolved ones <b>wholesale</b>, and a component
    /// given as <c>null</c> is removed (<see cref="RpgObjectResolve.WithState"/>).
    /// <para>
    /// The components are kept as written (<see cref="Components"/>), not as typed properties:
    /// "not named" and "named as null" must stay different through every serialisation the
    /// engine does (Extends, patches), and a typed nullable property cannot tell them apart.
    /// </para>
    /// </summary>
    public class RpgObjectState
    {
        /// <summary>Merge key (<c>States$add</c> by id); optional.</summary>
        public string Id { get; set; }

        /// <summary>Condition expression, evaluated in the placement's scope (<c>self.open == 1</c>).</summary>
        public string When { get; set; }

        /// <summary>
        /// The replaced components as written: any of <see cref="RpgObjectResolve.StateComponents"/>.
        /// </summary>
        [JsonExtensionData]
        [PatchLayer(typeof(RpgObject), Wholesale = true)]
        public Dictionary<string, JsonElement> Components { get; set; }
    }
}

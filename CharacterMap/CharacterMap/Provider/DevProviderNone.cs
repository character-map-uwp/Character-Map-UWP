using CharacterMap.Helpers;
using CharacterMap.Models;
using System.Collections.Generic;

namespace CharacterMap.Provider
{
    public class DevProviderNone : DevProviderBase
    {
        public DevProviderNone(CharacterRenderingOptions r, Character character) : base(r, character)
        {
            DisplayName = Localization.Get(ResourceKey);
        }

        public override IReadOnlyList<DevOption> GetAllOptions() => new List<DevOption>();

        protected override DevProviderType GetDevProviderType() => DevProviderType.None;

        protected override IReadOnlyList<DevOption> OnGetContextOptions() => null;

        protected override IReadOnlyList<DevOption> OnGetOptions() => null;
    }
}

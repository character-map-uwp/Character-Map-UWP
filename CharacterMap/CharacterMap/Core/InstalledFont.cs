using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CharacterMapCX;
using Microsoft.Graphics.Canvas.Text;
using Windows.Storage;
using Windows.UI.Text;

namespace CharacterMap.Core
{
    public class InstalledFont : IComparable, IEquatable<InstalledFont>
    {
        private List<FontVariant> _variants;

        DWriteFontFace _face { get; init; }

        public string Name { get; }

        public CanvasFontFace FontFace => _face.FontFace;

        public bool IsSymbolFont => _face.Properties.IsSymbolFont;

        public IList<FontVariant> Variants => _variants;

        public bool HasVariants => _variants.Count > 1;

        public bool HasImportedFiles { get; private set; }

        private InstalledFont(string name)
        {
            Name = name;
            _variants = new List<FontVariant>();
        }

        public InstalledFont(string name, DWriteFontFace face, StorageFile file = null) : this(name)
        {
            _face = face;
            AddVariant(face, file);
        }

        public FontVariant DefaultVariant
        {
            get
            {
                return Variants.FirstOrDefault(v => v.DirectWriteProperties.Weight.Weight == FontWeights.Normal.Weight && v.DirectWriteProperties.Style == FontStyle.Normal && v.DirectWriteProperties.Stretch == FontStretch.Normal) 
                    ?? Variants.FirstOrDefault(v => v.DirectWriteProperties.Weight.Weight == FontWeights.Normal.Weight && v.DirectWriteProperties.Style == FontStyle.Normal)
                    ?? Variants.FirstOrDefault(v => v.DirectWriteProperties.Weight.Weight == FontWeights.Normal.Weight && v.DirectWriteProperties.Stretch == FontStretch.Normal)
                    ?? Variants.FirstOrDefault(v => v.DirectWriteProperties.Weight.Weight == FontWeights.Normal.Weight)
                    ?? Variants[0];
            }
        }

        public void AddVariant(DWriteFontFace fontFace, StorageFile file = null)
        {
            _variants.Add(new FontVariant(fontFace, file, fontFace.Properties));

            if (file != null)
                HasImportedFiles = true;
        }

        public void SortVariants()
        {
            _variants = _variants.OrderBy(v => v.DirectWriteProperties.Weight.Weight).ToList();
        }

        public void PrepareForDelete()
        {
            //FontFace = null;
        }

        public InstalledFont Clone()
        {
            return new InstalledFont(this.Name)
            {
                _face = this._face,
                _variants = this._variants.ToList(),
                HasImportedFiles = this.HasImportedFiles
            };
        }

        public static InstalledFont CreateDefault(DWriteFontFace face)
        {
            var font = new InstalledFont("") { _face = face } ;
            //font._variants.Add(FontVariant.CreateDefault(face.FontFace));
            return font;
        }

        public int CompareTo(object obj)
        {
            if (obj is InstalledFont f)
                return Name.CompareTo(f.Name);

            return 0;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as InstalledFont);
        }

        public bool Equals(InstalledFont other)
        {
            return other is not null &&
                   Name == other.Name;
        }

        public override int GetHashCode()
        {
            int hashCode = -1425556920;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<CanvasFontFace>.Default.GetHashCode(FontFace);
            hashCode = hashCode * -1521134295 + EqualityComparer<IList<FontVariant>>.Default.GetHashCode(Variants);
            hashCode = hashCode * -1521134295 + HasImportedFiles.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<FontVariant>.Default.GetHashCode(DefaultVariant);
            return hashCode;
        }

        public static bool operator ==(InstalledFont left, InstalledFont right)
        {
            return EqualityComparer<InstalledFont>.Default.Equals(left, right);
        }

        public static bool operator !=(InstalledFont left, InstalledFont right)
        {
            return !(left == right);
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace BoundlessModelToObj
{
    [Serializable]
    public class SColor : IComparable<SColor>
    {
        public static SColor[] FromHtmlArray(string[] htmlArray)
        {
            return htmlArray.Select(cur => new SColor { XmlValue = cur }).ToArray();
        }

        public int CompareTo(SColor other)
        {
            int result;

            if ((result = Comparer<byte>.Default.Compare(R, other.R)) != 0)
            {
                return result;
            }
            if ((result = Comparer<byte>.Default.Compare(G, other.G)) != 0)
            {
                return result;
            }

            return Comparer<byte>.Default.Compare(B, other.B);
        }

        [XmlIgnore]
        public byte R { get; set; }

        [XmlIgnore]
        public byte G { get; set; }

        [XmlIgnore]
        public byte B { get; set; }

        [XmlIgnore]
        public byte A { get; set; } = 255;

        [XmlText]
        [Browsable(false)]
        public string XmlValue
        {
            get
            {
                Color col = Color.FromArgb(A, R, G, B);
                return ColorTranslator.ToHtml(col);
            }
            set
            {
                Color col = ColorTranslator.FromHtml(value);
                R = col.R;
                G = col.G;
                B = col.B;
                A = col.A;
            }
        }

        public override int GetHashCode()
        {
            return R.GetHashCode() ^ G.GetHashCode() ^ B.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SColor other))
            {
                return false;
            }

            return ((R == other.R) &&
                    (G == other.G) &&
                    (B == other.B));
        }
    }
}

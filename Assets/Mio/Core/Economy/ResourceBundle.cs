using System.Collections.Generic;
using System.Text;

namespace Mio.Core.Economy
{
    /// <summary>
    /// A small, allocation-light bag of the three resource families.
    /// Fixed-size on purpose: there are exactly three, and a struct keeps
    /// reward maths free of dictionary churn during play.
    /// </summary>
    public struct ResourceBundle
    {
        public int Cotton;
        public int Wood;
        public int Metal;

        public ResourceBundle(int cotton, int wood, int metal)
        {
            Cotton = cotton;
            Wood = wood;
            Metal = metal;
        }

        public static ResourceBundle Empty => new ResourceBundle(0, 0, 0);

        public bool IsEmpty => Cotton == 0 && Wood == 0 && Metal == 0;

        public int this[ResourceKind kind]
        {
            get
            {
                switch (kind)
                {
                    case ResourceKind.Cotton: return Cotton;
                    case ResourceKind.Wood: return Wood;
                    case ResourceKind.Metal: return Metal;
                    default: return 0;
                }
            }
            set
            {
                switch (kind)
                {
                    case ResourceKind.Cotton: Cotton = value; break;
                    case ResourceKind.Wood: Wood = value; break;
                    case ResourceKind.Metal: Metal = value; break;
                }
            }
        }

        public static ResourceBundle operator +(ResourceBundle a, ResourceBundle b)
        {
            return new ResourceBundle(
                a.Cotton + b.Cotton,
                a.Wood + b.Wood,
                a.Metal + b.Metal);
        }

        public static ResourceBundle operator *(ResourceBundle a, int scalar)
        {
            return new ResourceBundle(a.Cotton * scalar, a.Wood * scalar, a.Metal * scalar);
        }

        public void CopyInto(IDictionary<ResourceKind, int> target)
        {
            if (target == null) return;
            foreach (var kind in ResourceKinds.All)
            {
                target[kind] = this[kind];
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Cotton ").Append(Cotton);
            sb.Append(", Wood ").Append(Wood);
            sb.Append(", Metal ").Append(Metal);
            return sb.ToString();
        }
    }
}

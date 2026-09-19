using System.Collections.Generic;
using System.Text;

namespace Mio.Core.Economy
{
    /// <summary>
    /// A small, allocation-light bag of the three placeholder resources.
    /// Fixed-size on purpose: M0 has exactly three, and a struct keeps reward
    /// maths free of dictionary churn during play.
    /// </summary>
    public struct ResourceBundle
    {
        public int Energy;
        public int Material;
        public int Coin;

        public ResourceBundle(int energy, int material, int coin)
        {
            Energy = energy;
            Material = material;
            Coin = coin;
        }

        public static ResourceBundle Empty => new ResourceBundle(0, 0, 0);

        public bool IsEmpty => Energy == 0 && Material == 0 && Coin == 0;

        public int this[ResourceKind kind]
        {
            get
            {
                switch (kind)
                {
                    case ResourceKind.Energy: return Energy;
                    case ResourceKind.Material: return Material;
                    case ResourceKind.Coin: return Coin;
                    default: return 0;
                }
            }
            set
            {
                switch (kind)
                {
                    case ResourceKind.Energy: Energy = value; break;
                    case ResourceKind.Material: Material = value; break;
                    case ResourceKind.Coin: Coin = value; break;
                }
            }
        }

        public static ResourceBundle operator +(ResourceBundle a, ResourceBundle b)
        {
            return new ResourceBundle(
                a.Energy + b.Energy,
                a.Material + b.Material,
                a.Coin + b.Coin);
        }

        public static ResourceBundle operator *(ResourceBundle a, int scalar)
        {
            return new ResourceBundle(a.Energy * scalar, a.Material * scalar, a.Coin * scalar);
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
            sb.Append("Energy ").Append(Energy);
            sb.Append(", Material ").Append(Material);
            sb.Append(", Coin ").Append(Coin);
            return sb.ToString();
        }
    }
}

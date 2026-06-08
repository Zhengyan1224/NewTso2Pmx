using System;
using System.Collections.Generic;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

using TDCG;

namespace Tso2Pmd
{
    public class T2PTextureList
    {
        List<PMD_Texture> items = new List<PMD_Texture>();
        Dictionary<string, PMD_Texture> map = new Dictionary<string, PMD_Texture>();

        public readonly bool use_spheremap;

        public T2PTextureList(bool use_spheremap)
        {
            this.use_spheremap = use_spheremap;
        }

        public string[] GetFileNameList()
        {
            string[] names = new string[items.Count];
            int i = 0;
            foreach (PMD_Texture tex in items)
            {
                names[i++] = tex.FileName;
            }
            return names;
        }

        public void Add(TSOTex tso_tex, int tso_num)
        {
            if (tso_tex.width == 0 || tso_tex.height == 0)
                return;

            Image<Rgba32> image = CreateImage(tso_tex);
            PMD_Texture tex = new PMD_Texture(GenName(tso_num, tso_tex.Name));
            tex.Bitmap = image;

            foreach (PMD_Texture other in items)
            {
                if (other.Bitmap != null && EqualBitmaps(image, other.Bitmap))
                {
                    map[tex.Code] = other;

                    if (use_spheremap && other.IsToon)
                    {
                        PMD_Texture tex_sphere = new PMD_Texture(GenName(tso_num, tso_tex.Name), tex);
                        map[tex_sphere.Code] = other.Sphere!;
                    }
                    return;
                }
            }

            tex.SetID((sbyte)items.Count);
            items.Add(tex);
            map[tex.Code] = tex;

            if (use_spheremap && tex.IsToon)
            {
                PMD_Texture tex_sphere = new PMD_Texture(GenName(tso_num, tso_tex.Name), tex);
                tex_sphere.SetID((sbyte)items.Count);
                items.Add(tex_sphere);
                map[tex_sphere.Code] = tex_sphere;
            }
        }

        public void Save(string dest_path)
        {
            foreach (PMD_Texture texture in items)
            {
                texture.Save(dest_path);
            }
        }

        private static Image<Rgba32> CreateImage(TSOTex texture)
        {
            Image<Rgba32> image = new Image<Rgba32>(texture.width, texture.height);

            switch (texture.depth)
            {
                case 4:
                    using (Image<Bgra32> source = Image.LoadPixelData<Bgra32>(texture.data, texture.width, texture.height))
                    {
                        for (int y = 0; y < texture.height; y++)
                        {
                            for (int x = 0; x < texture.width; x++)
                            {
                                Bgra32 pixel = source[x, y];
                                image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, pixel.A);
                            }
                        }
                    }
                    break;
                case 3:
                    using (Image<Bgr24> source = Image.LoadPixelData<Bgr24>(texture.data, texture.width, texture.height))
                    {
                        for (int y = 0; y < texture.height; y++)
                        {
                            for (int x = 0; x < texture.width; x++)
                            {
                                Bgr24 pixel = source[x, y];
                                image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, byte.MaxValue);
                            }
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unsupported texture depth: {texture.depth}");
            }

            return image;
        }

        private static bool EqualBitmaps(Image<Rgba32> bmp1, Image<Rgba32> bmp2)
        {
            if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height)
                return false;

            for (int y = 0; y < bmp1.Height; y++)
            {
                for (int x = 0; x < bmp1.Width; x++)
                {
                    if (!bmp1[x, y].Equals(bmp2[x, y]))
                        return false;
                }
            }

            return true;
        }

        string GenName(int tso_num, string name)
        {
            return tso_num.ToString() + "-" + name;
        }

        string GenBitmapCode(int tso_num, string name)
        {
            return GenName(tso_num, name) + ".bmp";
        }

        string GenSphereCode(int tso_num, string name)
        {
            return GenName(tso_num, name) + ".sph";
        }

        public sbyte GetBitmapID(int tso_num, string name)
        {
            string code = GenBitmapCode(tso_num, name);
            if (map.TryGetValue(code, out PMD_Texture? tex))
                return tex.ID;
            else
                return -1;
        }

        public sbyte GetSphereID(int tso_num, string name)
        {
            string code = GenSphereCode(tso_num, name);
            if (map.TryGetValue(code, out PMD_Texture? tex))
                return tex.ID;
            else
                return -1;
        }
    }

    public class PMD_Texture
    {
        private static readonly BmpEncoder BmpWithTransparencyEncoder = new()
        {
            BitsPerPixel = BmpBitsPerPixel.Pixel32,
            SupportTransparency = true
        };

        sbyte id;
        string name;
        Image<Rgba32>? bmp;
        PMD_Texture? toon = null;
        PMD_Texture? sphere = null;

        public sbyte ID { get { return id; } }

        public void SetID(sbyte id)
        {
            this.id = id;
        }

        public Image<Rgba32>? Bitmap { get { return bmp; } set { bmp = value; } }

        public PMD_Texture? Sphere { get { return sphere; } }

        public void SetSphere(PMD_Texture sphere)
        {
            this.sphere = sphere;
        }

        public PMD_Texture(string name)
        {
            this.name = name;
        }

        public PMD_Texture(string name, PMD_Texture toon)
        {
            this.name = name;
            this.toon = toon;
            toon.SetSphere(this);
        }

        public bool IsToon
        {
            get { return bmp != null && bmp.Width == 256 && bmp.Height == 16; }
        }

        public bool IsSphere { get { return toon != null; } }

        public string FileExtension
        {
            get { return IsSphere ? ".sph" : ".bmp"; }
        }

        public string Code
        {
            get { return name + FileExtension; }
        }

        public string FileName
        {
            get { return string.Format("t{0:D3}", ID) + FileExtension; }
        }

        public void Save(string dest_path)
        {
            Image<Rgba32>? saved;

            if (IsSphere)
            {
                if (toon?.Bitmap == null)
                    return;

                saved = MakeSphereBitmap(toon.Bitmap);
            }
            else
            {
                saved = bmp;
                if (saved == null)
                    return;

                if (IsToon)
                    saved = TurnBitmap(saved);
            }

            string output = Path.Combine(dest_path, FileName);
            saved.Save(output, BmpWithTransparencyEncoder);
        }

        Image<Rgba32> TurnBitmap(Image<Rgba32> bmp1)
        {
            Image<Rgba32> bmp2 = new Image<Rgba32>(16, 250);

            for (int i = 0; i < 250; i++)
            {
                Rgba32 c = bmp1[i, 0];

                for (int j = 0; j < 16; j++)
                    bmp2[j, 250 - (i + 1)] = c;
            }

            return bmp2;
        }

        Image<Rgba32> MakeSphereBitmap(Image<Rgba32> bmp1)
        {
            Image<Rgba32> bmp2 = new Image<Rgba32>(1, 1);
            bmp2[0, 0] = bmp1[249, 0];
            return bmp2;
        }
    }
}

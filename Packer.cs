//-----------------------------------------------------------------------
//    Packer.cs: SpritePacker
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpritePacker
{
    public partial class Packer : Form
    {
        public int[] Sizes = new int[]
        {
            32,
            64,
            128,
            256,
            512,
            1024,
            2048,
            4096,
            8192,
        };

        public List<float> Scales = new List<float>
        {
            0.25f,
            0.5f,
            1,
        };

        string[] args;
        private bool isUnity = false;
        public Packer(string[] args)
        {
            InitializeComponent();

            comboBox2.DataSource = Scales;
            comboBox2.SelectedIndex = 2;

            if (args.Length == 3)
            {
                this.args = args;
                Shown += Packer_AfterShown;
            }
        }

        private void Packer_AfterShown(object sender, EventArgs e)
        {
            isUnity = true;
            args = args.Select((s) => s.Remove(0, 1)).ToArray();
            float scale = float.Parse(args[2]);
            comboBox2.SelectedIndex = Scales.IndexOf(scale);

            Refresh();

            Export(args[0], args[1], scale);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog openDialog = new FolderBrowserDialog();
            openDialog.Description = "Select the folder where the images you want to pack are.";

            if (openDialog.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(openDialog.SelectedPath))
                return;

            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = "png|*.png";
            saveDialog.Title = "Save Sprite";
            saveDialog.FileName = Path.GetFileName(openDialog.SelectedPath) + ".png";

            if (saveDialog.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(saveDialog.FileName))
                return;

            Export(openDialog.SelectedPath, saveDialog.FileName, (float)comboBox2.SelectedValue);
        }

        private void LockControls()
        {
            comboBox2.Enabled = false;
            button1.Enabled = false;
            label2.Enabled = false;
            Cursor = Cursors.WaitCursor;
            Refresh();
        }

        private void UnlockControls()
        {
            comboBox2.Enabled = true;
            button1.Enabled = true;
            label2.Enabled = true;
            Cursor = Cursors.Default;
            Refresh();
        }

        private void AddText(object text)
        {
            textBox2.AppendText(text.ToString() + "\n");
        }

        private void Error(string message)
        {
            if (!isUnity)
            {
                UnlockControls();
                AddText("Error: " + message);
                System.Media.SystemSounds.Hand.Play();
            }
            else
            {
                Console.WriteLine("Error: " + message);
                Environment.Exit(0);
            }
        }

        private void Export(string folderPath, string savePath, float scale)
        {
            string[] imagesPath = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);

            if (imagesPath.Length <= 0)
            {
                Error("this folder contains no images!");
                return;
            }

            LockControls();

            try
            {
                List<SpriteInfo> spriteInfos = new List<SpriteInfo>();
                AddText("Optimizing Images..");

                for (int i = 0; i < imagesPath.Length; i++)
                {
                    AddText("Optimizing Images.... " + (i + 1) * 100 / imagesPath.Length + "%");

                    Bitmap bmp = OptimizeImage(new Bitmap(Image.FromFile(imagesPath[i])), scale);

                    if (bmp.Width > 0 && bmp.Height > 0)
                    {
                        spriteInfos.Add(new SpriteInfo() { name = Path.GetFileNameWithoutExtension(imagesPath[i]), image = bmp });
                    }
                }

                if (spriteInfos.Count <= 0)
                {
                    Error("this folder contains no vaild images!");
                    return;
                }

                AddText("Creating Sprite Sheet..");

                int width = 0;
                int height = 0;
                int padding = 2;
                int tallestHeight = 0;

                int columnCount = GetColumnCount(spriteInfos);
                int itemIndex = 0;

                Bitmap spriteSheet = new Bitmap(8192, 8192);
                for (int i = 0; i < spriteInfos.Count; i++)
                {
                    if (itemIndex < columnCount)
                    {
                        itemIndex += 1;
                        if (tallestHeight < spriteInfos[i].image.Height)
                            tallestHeight = spriteInfos[i].image.Height;

                        width += spriteInfos[i].image.Width + padding;
                    }
                    else if (itemIndex >= columnCount)
                    {
                        itemIndex = 1;
                        width = 0;
                        height += tallestHeight;
                        tallestHeight = spriteInfos[i].image.Height;

                        width += spriteInfos[i].image.Width + padding;
                    }

                    spriteInfos[i].width = spriteInfos[i].image.Width + padding;
                    spriteInfos[i].height = spriteInfos[i].image.Height;

                    DrawImage(spriteSheet, spriteInfos[i].image, new Point(width, height));
                }

                spriteSheet = OptimizeImage(spriteSheet, 1);
                int bestSize = Sizes.FirstOrDefault((i) => spriteSheet.Width <= i && spriteSheet.Height <= i);

                if(bestSize > 1)
                {
                    AddText("Sprite Sheet Size: " + bestSize);
                    spriteSheet = ResizeImage(spriteSheet, bestSize);
                }

                AddText("Saving Sprite Sheet..");

                if (File.Exists(savePath))
                    File.Delete(savePath);

                spriteSheet.Save(savePath, ImageFormat.Png);

                if (isUnity)
                {
                    AddText("Creating Atlas for Unity Sprite..");

                    System.Text.StringBuilder unityOutput = new System.Text.StringBuilder();

                    unityOutput.Append(spriteSheet.Width);
                    unityOutput.Append("&&");
                    unityOutput.Append(columnCount);
                    unityOutput.Append("&&");

                    for (int i = 0; i < spriteInfos.Count; i++)
                    {
                        unityOutput.Append(spriteInfos[i].name + ";");
                        unityOutput.Append(spriteInfos[i].width + "," + spriteInfos[i].height);

                        if(i != spriteInfos.Count - 1)
                            unityOutput.Append("&&");
                    }

                    Console.WriteLine(unityOutput);

                    Environment.Exit(0);
                }

                AddText("Done");
                UnlockControls();
            }
            catch (Exception e)
            {
                Error(e.ToString());
            }
        }

        private int GetColumnCount(List<SpriteInfo> images)
        {
            int columnCount = 0;
            int size = 0;
            int width = 0;

            for (int i = 0; i < images.Count; i++)
            {
                size += images[i].image.Width * images[i].image.Height;
            }

            size = (int)Math.Sqrt(size) + ((size / images.Count) / 1000);

            for (int i = 0; i < images.Count; i++)
            {
                if(width + images[i].image.Width < size)
                {
                    columnCount += 1;
                    width += images[i].image.Width;
                }

            }

            if (columnCount == 0)
                columnCount = 10;

            return columnCount;
        }

        private Bitmap OptimizeImage(Bitmap bmp, float scale)
        {
            Rectangle bmpRect = default(Rectangle);
            BitmapData bmpData = null;

            bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] buffer = new byte[bmpData.Height * bmpData.Stride];
            Marshal.Copy(bmpData.Scan0, buffer, 0, buffer.Length);

            int xMin = int.MaxValue,
                xMax = int.MinValue,
                yMin = int.MaxValue,
                yMax = int.MinValue;

            bool foundPixel = false;

            for (int x = 0; x < bmpData.Width; x++)
            {
                bool stop = false;
                for (int y = 0; y < bmpData.Height; y++)
                {
                    byte alpha = buffer[y * bmpData.Stride + 4 * x + 3];
                    if (alpha != 0)
                    {
                        xMin = x;
                        stop = true;
                        foundPixel = true;
                        break;
                    }
                }
                if (stop)
                    break;
            }

            if (!foundPixel)
                return null;

            for (int y = 0; y < bmpData.Height; y++)
            {
                bool stop = false;
                for (int x = xMin; x < bmpData.Width; x++)
                {
                    byte alpha = buffer[y * bmpData.Stride + 4 * x + 3];
                    if (alpha != 0)
                    {
                        yMin = y;
                        stop = true;
                        break;
                    }
                }
                if (stop)
                    break;
            }

            for (int x = bmpData.Width - 1; x >= xMin; x--)
            {
                bool stop = false;
                for (int y = yMin; y < bmpData.Height; y++)
                {
                    byte alpha = buffer[y * bmpData.Stride + 4 * x + 3];
                    if (alpha != 0)
                    {
                        xMax = x;
                        stop = true;
                        break;
                    }
                }
                if (stop)
                    break;
            }

            for (int y = bmpData.Height - 1; y >= yMin; y--)
            {
                bool stop = false;
                for (int x = xMin; x <= xMax; x++)
                {
                    byte alpha = buffer[y * bmpData.Stride + 4 * x + 3];
                    if (alpha != 0)
                    {
                        yMax = y;
                        stop = true;
                        break;
                    }
                }
                if (stop)
                    break;
            }

            bmpRect = Rectangle.FromLTRB(xMin, yMin, xMax + 1, yMax + 1);

            if (bmpData != null)
                bmp.UnlockBits(bmpData);

            int scaledWidth = Convert.ToInt32(bmpRect.Width * scale);
            int scaledHeight = Convert.ToInt32(bmpRect.Height * scale);

            Bitmap target = new Bitmap(scaledWidth, scaledHeight);
            Rectangle targetRect = new Rectangle(0, 0, scaledWidth, scaledHeight);
            using (Graphics graphics = Graphics.FromImage(target))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(bmp, targetRect, bmpRect, GraphicsUnit.Pixel);
            }
            return target;
        }

        private Bitmap ResizeImage(Bitmap bmp, int size)
        {
            Bitmap target = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(target))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(bmp, new Rectangle(0, 0, size, size), new Rectangle(0, 0, size, size), GraphicsUnit.Pixel);
            }
            return target;
        }

        private Bitmap DrawImage(Bitmap spriteSheet, Bitmap bmp, Point point)
        {
            using (Graphics graphics = Graphics.FromImage(spriteSheet))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(bmp, point);
            }
            return spriteSheet;
        }
    }

    public class SpriteInfo
    {
        public Bitmap image;
        public string name;
        public int width;
        public int height;
    }

    public class Vertex
    {
        public int x;
        public int y;

        public Vertex(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
}

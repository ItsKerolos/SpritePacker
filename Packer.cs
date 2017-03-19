using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

public class SpritePacker
{
    private static bool isUnity = false;
    private static int[] PowerOf2 = new int[]
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

    private static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Invaild format.\nExample: SpritePacker input_folder output_file scale\n         SpritePacker \"my assets/folder\" \"mygame/output.png\" 0.5");
            return;
        }

        if (args.Length >= 4 && args[3] == "unity")
            isUnity = true;

        try
        {
            if (!Directory.Exists(args[0]) || Directory.GetFiles(args[0], "*.png", SearchOption.TopDirectoryOnly).Length <= 0)
            {
                Error("this folder contains no images!");
                return;
            }

            Export(args[0], args[1], args.Length == 2 ? 1 : float.Parse(args[2]));
        }
        catch(Exception e)
        {
            Error(e.Message);
        }
    }   

    #region API

    private static void Export(string folderPath, string savePath, float scale)
    {
        string[] imagesPath = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);

        try
        {
            List<SpriteInfo> spriteInfos = new List<SpriteInfo>();
            Console.WriteLine("Optimizing Images..");

            for (int i = 0; i < imagesPath.Length; i++)
            {
                Console.WriteLine("Optimizing Images.... " + (i + 1) * 100 / imagesPath.Length + "%");

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

            Console.WriteLine("Creating Sprite Sheet..");

            int x = 0;
            int lastX = 0;
            int y = 0;
            int highestY = 0;
            int padding = 2;

            int columnCount = GetColumnCount(spriteInfos);
            int itemIndex = 0;

            Bitmap spriteSheet = new Bitmap(8192, 8192);
            for (int i = 0; i < spriteInfos.Count; i++)
            {
                if (itemIndex < columnCount)
                {
                    itemIndex += 1;
                    if (highestY < spriteInfos[i].image.Height)
                        highestY = spriteInfos[i].image.Height;

                    if (itemIndex == 1)
                    {
                        x = padding;
                        y = padding;
                    }
                    else
                    {
                        x += lastX + padding;
                    }
                }
                else
                {
                    itemIndex = 1;
                    lastX = 0;

                    x = padding;
                    y += highestY + padding;
                    highestY = spriteInfos[i].image.Height;
                }

                lastX = spriteInfos[i].image.Width;
                spriteInfos[i].width = spriteInfos[i].image.Width;
                spriteInfos[i].height = spriteInfos[i].image.Height;

                DrawImage(spriteSheet, spriteInfos[i].image, new Rectangle(x, y, spriteInfos[i].width, spriteInfos[i].height));
            }

            Bitmap t = OptimizeImage(spriteSheet, 1);
            int bestSize = PowerOf2.FirstOrDefault((i) => t.Width + padding <= i && t.Height + padding <= i);

            if (bestSize > 1)
            {
                Console.WriteLine("Sprite Sheet Size: " + bestSize);
                spriteSheet = ResizeImage(spriteSheet, bestSize);
            }

            Console.WriteLine("Saving Sprite Sheet..");

            if (File.Exists(savePath))
                File.Delete(savePath);

            spriteSheet.Save(savePath, ImageFormat.Png);

            if (isUnity)
            {
                Console.WriteLine("Creating Atlas for Unity Sprite..");

                System.Text.StringBuilder unityOutput = new System.Text.StringBuilder();

                unityOutput.Append(spriteSheet.Width);
                unityOutput.Append("&&");
                unityOutput.Append(columnCount);
                unityOutput.Append("&&");

                for (int i = 0; i < spriteInfos.Count; i++)
                {
                    unityOutput.Append(spriteInfos[i].name + ";");
                    unityOutput.Append(spriteInfos[i].width + "," + spriteInfos[i].height);

                    if (i != spriteInfos.Count - 1)
                        unityOutput.Append("&&");
                }

                Console.WriteLine(unityOutput);

                Console.WriteLine("Done.");
                Environment.Exit(0);
            }

            Console.WriteLine("Done.");
        }
        catch (Exception e)
        {
            Error(e.ToString());
        }
    }

    private static void Error(string message)
    {
        Console.WriteLine("Error: " + message);

        if (isUnity)
            Environment.Exit(0);
    }

    private static int GetColumnCount(List<SpriteInfo> images)
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
            if (width + images[i].image.Width < size)
            {
                columnCount += 1;
                width += images[i].image.Width;
            }

        }

        if (columnCount == 0)
            columnCount = 10;

        return columnCount;
    }

    private static Bitmap OptimizeImage(Bitmap bmp, float scale)
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

    private static Bitmap ResizeImage(Bitmap bmp, int size)
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

    private static Bitmap DrawImage(Bitmap spriteSheet, Bitmap bmp, Rectangle rect)
    {
        using (Graphics graphics = Graphics.FromImage(spriteSheet))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(bmp, rect);
        }

        return spriteSheet;
    }

    #endregion
}

#region Sub-Classes

public class SpriteInfo
{
    public Bitmap image;
    public string name;
    public int width;
    public int height;
}

#endregion

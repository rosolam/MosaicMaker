using SkiaSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MosaicMaker
{
    public class MosaicOld
    {
        private SKBitmap _sourceBitmap;
        const int pathCount = 200;
        const double minDiameterPct = .05;
        const double maxDiameterPct = .75;
        List<SKRegion> regions = new List<SKRegion>();

        public void Generate(string inFileName, string outFileName, List<SKRegion> preloadRegions = null)
        {

            // Load an image from a file (replace with your image path)
            _sourceBitmap = SKBitmap.Decode(@"C:\Users\micha\Downloads\" + inFileName);
            Console.WriteLine("image loaded @" + DateTime.Now);

            // create random regions
            if(preloadRegions != null)
            {
                regions = new List<SKRegion>(preloadRegions);
            }
            else
            {
                Random random = new Random();
                for (int i = 0; i < pathCount; i++)
                {

                    int rndX = random.Next(0, _sourceBitmap.Width);
                    int rndY = random.Next(0, _sourceBitmap.Height);
                    int rndD = random.Next((int)(minDiameterPct * _sourceBitmap.Width / 2), (int)(maxDiameterPct * _sourceBitmap.Width / 2));
                    SKPath path = new SKPath();
                    path.AddCircle(rndX, rndY, rndD);
                    SKRegion region = new SKRegion(path);
                    regions.Add(region);

                }
            }
            Console.WriteLine("regions created @" + DateTime.Now);
            //DrawImage(fileName + "_debug");

            // split up the regions
            bool isMoreIntersections = true;
            do
            {
                isMoreIntersections = IntersectRegions();
            } while (isMoreIntersections);
            Console.WriteLine("regions split " + regions.Count.ToString() +  "  @" + DateTime.Now);


            DrawImage(outFileName);
            Console.WriteLine("image saved @" + DateTime.Now);


        }

        private void DrawImage(string name)
        {

            // Draw
            SKBitmap bitmap = new SKBitmap(_sourceBitmap.Width, _sourceBitmap.Height);
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                using (var paint = new SKPaint())
                {
                    foreach (SKRegion r in regions)
                    {
                        //paint.Color = GetRandomColor();
                        paint.Color = GetAverageColor(_sourceBitmap, r);
                        canvas.DrawRegion(r, paint);
                    }
                }
            }

            // Encode the SKBitmap to SKData
            SKImage image = SKImage.FromBitmap(bitmap);
            SKData imageData = image.Encode();

            // Save SKData to a file
            string filePath = @"C:\Users\micha\Downloads\" + name + ".png";
            File.WriteAllBytes(filePath, imageData.ToArray());

        }

        private void DebugRegions(string name, List<SKRegion> debugRegions)
        {

            // Draw
            SKBitmap bitmap = new SKBitmap(_sourceBitmap.Width, _sourceBitmap.Height);
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                using (var paint = new SKPaint())
                {
                    foreach (SKRegion r in debugRegions)
                    {
                        paint.Color = GetRandomColor();
                        //paint.Color = GetAverageColor(_sourceBitmap, r);
                        canvas.DrawRegion(r, paint);
                    }
                }
            }

            // Encode the SKBitmap to SKData
            SKImage image = SKImage.FromBitmap(bitmap);
            SKData imageData = image.Encode();

            // Save SKData to a file
            string filePath = @"C:\Users\micha\Downloads\" + name + ".png";
            File.WriteAllBytes(filePath, imageData.ToArray());

        }

        private SKColor GetRandomColor()
        {

            // Create a Random object
            Random random = new Random();

            // Generate random RGB values
            byte red = (byte)random.Next(256);
            byte green = (byte)random.Next(256);
            byte blue = (byte)random.Next(256);

            // Create a random SKColor
            return new SKColor(red, green, blue);

        }

        private bool IntersectRegions()
        {

            foreach (SKRegion r1 in regions)
            {

                foreach (SKRegion r2 in regions)
                {

                    if(r1 == r2 || r1.Equals(r2) || r1.Bounds.Equals(r2.Bounds)) continue;

                    if (r1.Intersects(r2))
                    {

                        //List<SKRegion> debugRegions = new List<SKRegion>();

                        // difference it against region 2 to break into 3 parts (if result is non-empty)
                        SKRegion regionDiff = new SKRegion();
                        regionDiff.Op(r1, SKRegionOperation.Replace);
                        if (!regionDiff.Op(r2, SKRegionOperation.Difference))
                        {
                            continue;
                        }
                        SKRegion regionRevDiff = new SKRegion();
                        regionRevDiff.Op(r1, SKRegionOperation.Replace);
                        if (!regionRevDiff.Op(r2, SKRegionOperation.ReverseDifference))
                        {
                            continue;
                        }
                        SKRegion regionIntersect = new SKRegion();
                        regionIntersect.Op(r1, SKRegionOperation.Replace);
                        if (!regionIntersect.Op(r2, SKRegionOperation.Intersect))
                        {
                            continue;
                        }

                        //regions.Add(regionDiff);
                        regions.Insert(0, regionDiff);
                        //regions.Add(regionRevDiff);
                        regions.Insert(0, regionRevDiff);
                        //regions.Add(regionIntersect);
                        regions.Insert(0, regionIntersect);
                       
                        // remove the originals
                        regions.Remove(r1);
                        regions.Remove(r2);

                        // we have altered the list, so we need to start again
                        return true;

                    }


                }

            }

            // no more intersections
            return false;
        }

        private SKColor GetAverageColor(SKBitmap bitmap, SKRegion region)
        {
            int totalR = 0, totalG = 0, totalB = 0;
            int totalPoints = 0;

            SKRectI boundingRect = region.Bounds;
            
            int yStep = Math.Max((int)(bitmap.Height / 100),1);
            int yFrom = Math.Clamp(boundingRect.Top, 0, bitmap.Height - 1 - yStep);
            int yTo = Math.Clamp(boundingRect.Bottom, 0, bitmap.Height - yStep);
            int xStep = Math.Max((int)(bitmap.Width / 100),1);
            int xFrom = Math.Clamp(boundingRect.Left, 0, bitmap.Width - 1 - xStep);
            int xTo = Math.Clamp(boundingRect.Right, 0, bitmap.Width - xStep);           

            for (int y = yFrom; y < yTo; y+=yStep)
            {
                for (int x = xFrom; x < xTo; x += xStep)
                {
                    if (region.Contains(x, y))
                    {
                        SKColor pixelColor = bitmap.GetPixel(x, y);
                        totalR += pixelColor.Red;
                        totalG += pixelColor.Green;
                        totalB += pixelColor.Blue;
                        totalPoints++;
                    }
                }
            }

            if(totalPoints == 0)
            {
                return SKColor.Empty;
            }
           
            // Calculate the average color components
            byte avgR = (byte)(totalR / totalPoints);
            byte avgG = (byte)(totalG / totalPoints);
            byte avgB = (byte)(totalB / totalPoints);

            return new SKColor(avgR, avgG, avgB);
        }

    }
}

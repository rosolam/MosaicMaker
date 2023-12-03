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
    public class Mosaic
    {
        private SKBitmap _sourceBitmap;
        public int PathCount = 100;
        public double MinDiameterPct = .05;
        public double MaxDiameterPct = .75;
        public List<SKRegion> Regions = new List<SKRegion>();
        public List<SKRegion> SplitRegions = new List<SKRegion>();

        public void BatchGenerate(string inPath, string processedPath, string outPath, int variationCount)
        {
            // Check if the folder exists
            if (Directory.Exists(inPath))
            {
                // Get an array of file paths in the folder
                string[] filePaths = Directory.GetFiles(inPath);

                // Iterate over the filenames
                foreach (string inFilePath in filePaths)
                {

                    // Process each filename
                    string fileName = Path.GetFileName(inFilePath);
                    Console.WriteLine($"Processing: {fileName}");

                    // generate mosaic
                    for (int i = 0; i < variationCount; i++)
                    {
                        string outFilePath = outPath + @"\" + Path.GetFileNameWithoutExtension(inFilePath) + "_mosaic_" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".png";
                        Generate(inFilePath, outFilePath);
                    }

                    // move to processed folder
                    File.Move(inFilePath, processedPath + @"\" + fileName);

                }
            }
        }

        public void Generate(string inFilePath, string outFilePath, List<SKRegion> preloadRegions = null)
        {

            // Load an image from a file (replace with your image path)
            _sourceBitmap = SKBitmap.Decode(inFilePath);
            Console.WriteLine("image loaded @" + DateTime.Now);

            // create random regions
            if (preloadRegions != null)
            {
                Regions = new List<SKRegion>(preloadRegions);
            }
            else
            {
                Regions = new List<SKRegion>();
                Random random = new Random();
                for (int i = 0; i < PathCount; i++)
                {

                    int rndX = random.Next(0, _sourceBitmap.Width);
                    int rndY = random.Next(0, _sourceBitmap.Height);
                    int rndD = random.Next((int)(MinDiameterPct * _sourceBitmap.Width / 2), (int)(MaxDiameterPct * _sourceBitmap.Width / 2));
                    SKPath path = new SKPath();
                    path.AddCircle(rndX, rndY, rndD);
                    SKRegion region = new SKRegion(path);
                    Regions.Add(region);

                }
            }
            Console.WriteLine("regions created @" + DateTime.Now);
            //DrawImage(fileName + "_debug",true);

            // split up the regions
            List<SKRegion> regionsToSplit = new List<SKRegion>(Regions);
            SplitRegions = new List<SKRegion>();
            do
            {

                // get first region on list
                SKRegion r = regionsToSplit[0];

                // split this region up based on other regions
                if (!IntersectRegions(regionsToSplit, r)) {

                    // r did not intersect with anything so it is done being split, let's move to the output list so we don't keep checking it 
                    regionsToSplit.Remove(r);
                    SplitRegions.Add(r);

                }

            } while (regionsToSplit.Count > 0);
            Console.WriteLine("regions split [" + SplitRegions.Count.ToString() +  "]  @" + DateTime.Now);


            DrawImage(outFilePath, SplitRegions);
            Console.WriteLine("image saved @" + DateTime.Now);


        }

        private void DrawImage(string outFilePath, List<SKRegion> drawRegions, bool rnd = false)
        {

            // Draw
            SKBitmap bitmap = new SKBitmap(_sourceBitmap.Width, _sourceBitmap.Height);
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                using (var paint = new SKPaint())
                {
                    foreach (SKRegion r in drawRegions)
                    {
                        if (rnd)
                        {
                            paint.Color = GetRandomColor();
                        }
                        else
                        {
                            paint.Color = GetAverageColor(_sourceBitmap, r);
                        }
                        canvas.DrawRegion(r, paint);
                    }
                }
            }

            // Encode the SKBitmap to SKData
            SKImage image = SKImage.FromBitmap(bitmap);
            SKData imageData = image.Encode();

            // Save SKData to a file
            File.WriteAllBytes(outFilePath, imageData.ToArray());

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

        private bool IntersectRegions(List<SKRegion> regionsToSplit, SKRegion r1)
        {

            // test intersecting it against all other regions            
            foreach (SKRegion r2 in regionsToSplit)
            {

                // if same region, same shape... and unfortunately same bounds because encountered situations where the same shape is not picked up by first two checks, not sure why, no resort but to compare bounds which is probably safe
                if (r1 == r2 || r1.Equals(r2) || r1.Bounds.Equals(r2.Bounds)) continue;

                // do they intersect?
                if (r1.Intersects(r2))
                {

                    // difference it against region 2 to break into 3 parts (r1 - r2, r2 - r1, r1 U r2)

                    // r1 - r2                    
                    SKRegion regionDiff = new SKRegion();
                    regionDiff.Op(r1, SKRegionOperation.Replace);
                    if(regionDiff.Op(r2, SKRegionOperation.Difference)) { regionsToSplit.Add(regionDiff); }

                    // r2 - r1
                    SKRegion regionRevDiff = new SKRegion();
                    regionRevDiff.Op(r1, SKRegionOperation.Replace);
                    if(regionRevDiff.Op(r2, SKRegionOperation.ReverseDifference)) { regionsToSplit.Add(regionRevDiff); }

                    // r1 XOR r2 (I shouldn't need to bother even checking there is a result because we know they overlap at this point but I will just in case there is an edge case where they overlap on a single point/line) 
                    SKRegion regionIntersect = new SKRegion();
                    regionIntersect.Op(r1, SKRegionOperation.Replace);
                    if (regionIntersect.Op(r2, SKRegionOperation.Intersect)){regionsToSplit.Add(regionIntersect);}
                  
                    // ... and remove the originals
                    regionsToSplit.Remove(r1);
                    regionsToSplit.Remove(r2);

                    return true;

                }
            }

            // no intersections
            return false;

        }

        private SKColor GetAverageColor(SKBitmap bitmap, SKRegion region)
        {
            int totalR = 0, totalG = 0, totalB = 0;
            int totalPoints = 0;

            SKRectI boundingRect = region.Bounds;

            int yStep = 1;// Math.Max((int)(boundingRect.Height / 100),1);
            int yFrom = Math.Clamp(boundingRect.Top, 0, bitmap.Height - 1 - yStep);
            int yTo = Math.Clamp(boundingRect.Bottom, 0, bitmap.Height - yStep);
            int xStep = 1; // Math.Max((int)(boundingRect.Width / 100),1);
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

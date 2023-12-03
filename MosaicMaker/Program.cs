// See https://aka.ms/new-console-template for more information

using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;
using MosaicMaker;

class Program
{

    static void Main()
    {

        Mosaic m = new Mosaic();

        string inPath = @"C:\Users\micha\Downloads\mosaic\in";
        string processedPath = @"C:\Users\micha\Downloads\mosaic\in\processed";
        string outPath = @"C:\Users\micha\Downloads\mosaic\out";
        int varationCount = 3;

        m.BatchGenerate(inPath, processedPath, outPath,varationCount);

        Console.WriteLine("done");
    }
}

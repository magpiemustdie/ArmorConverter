using SoulsFormats;
using ImageMagick;
using DirectXTexNet;
using System.Runtime.InteropServices;

using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Channels;

class CleanFolder
{
    public static void CleanMyFolder(string path2folder, string pattern)
    {
        string[] files = Directory.GetFiles(path2folder);

        foreach (string file in files)
        {
            string filename = Path.GetFileName(file);

            if (filename.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);
                Console.WriteLine($"Deleted file: {filename}");
            }
        }
    }
}

class DDSConv
{
    public static void dds2uncompressed(byte[] imageBytes, string imageName, bool dsr, string tempFolder)
    {
        byte[] compressedData = imageBytes;
        GCHandle array = GCHandle.Alloc(imageBytes, GCHandleType.Pinned);
        ScratchImage texImage = TexHelper.Instance.LoadFromDDSMemory(array.AddrOfPinnedObject(), imageBytes.Length, DDS_FLAGS.NONE);
        texImage = texImage.Decompress(DXGI_FORMAT.B8G8R8A8_UNORM);

        if (dsr == false)
        {
            texImage.SaveToTGAFile(0, $"{tempFolder}\\{imageName}_ptde.tga");
            array.Free();
        }
        else
        {
            texImage.SaveToTGAFile(0, $"{tempFolder}\\{imageName}_dsr.tga");
            array.Free();
        }
    }
}

class TextureConverter
{
    static public void FolderParser(string path2folder)
    {
        string[] files = Directory.GetFiles(path2folder);

        foreach (string file in files)
        {
            string filename = Path.GetFileName(file);

            if (filename.Contains("_ptde", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"{path2folder}\\{filename}");
                BC7texconv($"{path2folder}\\{filename}", $"{path2folder}", true);
            }
        }
    }

    static public void BC7texconv(string inputTexture, string outputTexture, bool conversion)
    {
        Console.WriteLine(inputTexture);
        if (conversion)
        {
            string arguments = $"-f BC7_UNORM {inputTexture} -o {outputTexture} -y";
            ExecuteTexConv(arguments);
        }
    }

    static void ExecuteTexConv(string arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "texconv.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"Failed to execute texconv. Exit code: {process.ExitCode}");
                    Console.WriteLine(output);
                }
                else
                {
                    Console.WriteLine("Texture conversion completed successfully.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}

class ImageMagickTest
{
    public static void LoadImageFromBytes(string imageName, byte[] imageBytes)
    {
        try
        {
            using (var image = new MagickImage(imageBytes))
            {
                Console.WriteLine($"Image loaded: {image.Width}x{image.Height}");
                image.Write(imageName + ".png");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load image: {ex.Message}");
        }
    }

    static public void SmapParser(string path2folder, string pattern)
    {
        string[] files = Directory.GetFiles(path2folder);
        string image1Path = "";
        string image2Path;

        foreach (string file in files)
        {
            string filename = Path.GetFileName(file);

            if (filename.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                image1Path = Path.Combine(path2folder, filename);
                Console.WriteLine($"Found spec_map: {image1Path}");
                image2Path = image1Path.Replace("_s_dsr", "_s_ptde");
                Red2alpha(image1Path, image2Path, image2Path);
            }
        }
    }

    static public void Red2alpha(string image1Path, string image2Path, string outputPath)
    {
        using (MagickImage image1 = new MagickImage(image1Path))
        {
            using (MagickImage redChannel = (MagickImage)image1.Separate(Channels.Red).First())
            {
                using (MagickImage image2 = new MagickImage(image2Path))
                {
                    image2.Alpha(AlphaOption.On);
                    image2.Composite(redChannel, CompositeOperator.CopyAlpha);
                    image2.Format = MagickFormat.Tga;
                    image2.Quality = 100;
                    image2.Write(outputPath);
                }
            }
        }
    }
}

class ModelLoader
{
    public static void MaterialNameReplacer(string path2model)
    {
        BND3 parts_model_bnd = BND3.Read(path2model);
        Console.WriteLine("MaterialNameReplacer -> Starting...");

        foreach (BinderFile binder_files in parts_model_bnd.Files)
        {
            if (binder_files.Name.EndsWith(".flver") || (binder_files.Name.EndsWith(".flver.dcx")))
            {
                Console.WriteLine(binder_files.Name);
                FLVER2 flver_model = FLVER2.Read(binder_files.Bytes);
                foreach (var mtd in flver_model.Materials)
                {
                    if (!mtd.MTD.ToLower().EndsWith("_spec.mtd"))
                    {
                        mtd.MTD = mtd.MTD.Replace(".mtd", "_Spec.mtd");
                    }
                }
                binder_files.Bytes = flver_model.Write();
            }
        }
        parts_model_bnd.Write(path2model);
        Console.WriteLine("MaterialNameReplacer -> Done");
    }

    public static void Get_textures(string path2model, bool dsr, string tempFolder)
    {
        Console.WriteLine(path2model);
        BND3 parts_model_bnd = BND3.Read(path2model);
        foreach (BinderFile binder_files in parts_model_bnd.Files)
        {
            if (binder_files.Name.EndsWith(".tpf") || binder_files.Name.EndsWith(".tpf.dcx"))
            {
                Console.WriteLine(binder_files.Name);

                TPF textures_bnd = TPF.Read(binder_files.Bytes);
                foreach (var texture in textures_bnd.Textures)
                {
                    byte[] imageBytes = texture.Bytes;
                    string imageName = texture.Name;
                    if (dsr)
                    {
                        Console.WriteLine("Extracting DSR textures:");
                        Console.WriteLine(texture.Name);
                        DDSConv.dds2uncompressed(imageBytes, imageName, dsr, tempFolder);
                        Console.WriteLine("Done");
                    }
                    else
                    {
                        Console.WriteLine("Extracting PTDE textures:");
                        Console.WriteLine(texture.Name);
                        DDSConv.dds2uncompressed(imageBytes, imageName, dsr, tempFolder);
                        Console.WriteLine("Done");
                    }
                }
            }
        }
    }

    public static void TextureReplacer(string path2model, string path2textures)
    {
        BND3 parts_model_bnd = BND3.Read(path2model);

        foreach (BinderFile binder_files in parts_model_bnd.Files)
        {
            if (binder_files.Name.EndsWith(".tpf") || (binder_files.Name.EndsWith(".tpf.dcx")))
            {
                Console.WriteLine(binder_files.Name);

                TPF textures_bnd = TPF.Read(binder_files.Bytes);
                foreach (var texture in textures_bnd.Textures)
                {
                    string imageName = texture.Name;
                    Console.WriteLine("Replacing texture in DSR bnd:");
                    Console.WriteLine(texture.Name);
                    string ddsFilePath = $"{path2textures}\\{imageName}_ptde.dds";

                    if (File.Exists(ddsFilePath))
                    {
                        texture.Bytes = File.ReadAllBytes(ddsFilePath);
                        Console.WriteLine($"Replaced: {imageName}");
                    }
                    else
                    {
                        Console.WriteLine($"File not found: {ddsFilePath}");
                    }
                }

                Console.WriteLine("Changing texture format:");
                foreach (var texture in textures_bnd.Textures)
                {
                    Console.WriteLine($"{texture.Format} -> 38");
                    texture.Format = 38;
                }
                binder_files.Bytes = textures_bnd.Write();
            }
        }
        parts_model_bnd.Write(path2model);
    }
}

class Program
{
    static void Main(string[] args)
    {
        string filter = "9300";
        string ptdepath = "";
        string dsrpath = "";
        string temppath = "";

        // Command line argument processing
        if (args.Length >= 1)
        {
            filter = args[0];
        }

        if (args.Length >= 4)
        {
            ptdepath = args[1];
            dsrpath = args[2];
            temppath = args[3];
        }
        else
        {
            // Interactive mode if not enough arguments
            Console.WriteLine("Enter file search filter (e.g., 9300):");
            filter = Console.ReadLine();

            Console.WriteLine("Enter PTDE folder path:");
            ptdepath = Console.ReadLine();

            Console.WriteLine("Enter DSR folder path:");
            dsrpath = Console.ReadLine();

            Console.WriteLine("Enter temporary folder path:");
            temppath = Console.ReadLine();
        }

        // Validate folder existence
        if (!Directory.Exists(ptdepath))
        {
            Console.WriteLine($"PTDE folder does not exist: {ptdepath}");
            return;
        }

        if (!Directory.Exists(dsrpath))
        {
            Console.WriteLine($"DSR folder does not exist: {dsrpath}");
            return;
        }

        if (!Directory.Exists(temppath))
        {
            try
            {
                Directory.CreateDirectory(temppath);
                Console.WriteLine($"Created temporary folder: {temppath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create temporary folder: {ex.Message}");
                return;
            }
        }

        Console.WriteLine($"Processing settings:");
        Console.WriteLine($"Filter: {filter}");
        Console.WriteLine($"PTDE path: {ptdepath}");
        Console.WriteLine($"DSR path: {dsrpath}");
        Console.WriteLine($"Temp path: {temppath}");
        Console.WriteLine("=============================");

        string[] files = Directory.GetFiles(dsrpath);

        foreach (var file in files)
        {
            try
            {
                if (file.Contains(filter))
                {
                    string modelname = file.Split('\\').Last();
                    modelname = modelname.Split(".partsbnd")[0];

                    Console.WriteLine($"Processing model: {modelname}");

                    // Extract textures from PTDE
                    string ptdeModelPath = Path.Combine(ptdepath, $"{modelname}.partsbnd.dcx");
                    if (File.Exists(ptdeModelPath))
                    {
                        ModelLoader.Get_textures(ptdeModelPath, false, temppath);
                    }
                    else
                    {
                        Console.WriteLine($"PTDE model not found: {ptdeModelPath}");
                    }

                    // Extract textures from DSR
                    string dsrModelPath = Path.Combine(dsrpath, $"{modelname}.partsbnd.dcx");
                    if (File.Exists(dsrModelPath))
                    {
                        ModelLoader.Get_textures(dsrModelPath, true, temppath);
                    }
                    else
                    {
                        Console.WriteLine($"DSR model not found: {dsrModelPath}");
                    }

                    // Process spec maps
                    ImageMagickTest.SmapParser(temppath, "_s_dsr");

                    // Clean temporary files
                    CleanFolder.CleanMyFolder(temppath, "_dsr");

                    // Convert textures
                    TextureConverter.FolderParser(temppath);

                    // Clean TGA files
                    CleanFolder.CleanMyFolder(temppath, ".tga");

                    // Replace textures in DSR model
                    if (File.Exists(dsrModelPath))
                    {
                        ModelLoader.TextureReplacer(dsrModelPath, temppath);
                    }

                    // Replace material names
                    if (File.Exists(dsrModelPath))
                    {
                        ModelLoader.MaterialNameReplacer(dsrModelPath);
                    }

                    // Clean DDS files
                    CleanFolder.CleanMyFolder(temppath, ".dds");

                    Console.WriteLine($"Model {modelname} processed successfully!");
                    Console.WriteLine("=============================");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {file}: {ex.Message}");
                Console.WriteLine("=============================");
            }
        }

        Console.WriteLine("Processing completed!");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
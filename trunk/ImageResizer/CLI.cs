﻿using System;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using ImageResizer.Properties;

using Imager;
using Imager.Interface;

namespace ImageResizer {
  internal static class CLI {

    /// <summary>
    /// An image loaded through the CLI
    /// </summary>
    private static Bitmap _currentImage;

    /// <summary>
    /// Parses the command line arguments.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    public static int ParseCommandLineArguments(string[] arguments) {
      if (arguments == null || arguments.Length < 1)
        return (0);

      var i = 0;
      var length = arguments.Length;
      while (i < length) {
        var command = arguments[i++].ToUpperInvariant();
        switch (command) {
          #region /LOAD
          case "/LOAD": {
            if (length - i < 1) {
              _ShowHelp();
              return (2);
            }
            var filename = arguments[i++];
            if (filename == null) {
              _ShowHelp();
              return (2);
            }
            _currentImage = (Bitmap)Image.FromFile(filename);
            break;
          }
          #endregion
          #region /RESIZE
          case "/RESIZE": {
            if (length - i < 2) {
              _ShowHelp();
              return (2);
            }
            var dimensions = arguments[i++].Trim();
            var filterName = arguments[i++].Trim();
            var positionOfX = dimensions.IndexOf('x');
            if (positionOfX <= 0) {
              _ShowHelp();
              return (2);
            }
            int intX, intY;
            if (!int.TryParse(dimensions.Substring(0, positionOfX), out intX) || !int.TryParse(dimensions.Substring(positionOfX + 1), out intY)) {
              _ShowHelp();
              return (2);
            }
            int repeat;
            positionOfX = filterName.IndexOf('(');
            if (positionOfX > 0 && filterName.EndsWith(")")) {
              if (!int.TryParse(filterName.Substring(positionOfX + 1, filterName.Length - positionOfX - 2), out repeat))
                repeat = 1;

              filterName = filterName.Substring(0, positionOfX);
              if (repeat < 1)
                repeat = 1;
            } else
              repeat = 1;
            if (_currentImage == null) {
              MessageBox.Show(Resources.txNothingToResize, Resources.ttNothingToResize, MessageBoxButtons.OK, MessageBoxIcon.Error);
              return (2);
            }
            Console.WriteLine(@"{0} {1} {2} {3}", intX, intY, repeat, filterName);
            var imageResizerTokens = Program.IMAGE_RESIZERS;
            Contract.Assume(imageResizerTokens != null);
            var matches = imageResizerTokens.Where(resizer => string.Compare(resizer.Name, filterName, true) == 0).ToArray();
            if (matches.Length <= 0) {
              MessageBox.Show(string.Format(Resources.txUnknownFilter, filterName), Resources.ttUnknownFilter, MessageBoxButtons.OK, MessageBoxIcon.Error);
              return (2);
            }
            for (var j = 0; j < repeat; j++) {
              var image = _currentImage;
              _currentImage = Program.FilterAndResizeImage(image, matches[0], intX, intY, OutOfBoundsMode.ConstantExtension, OutOfBoundsMode.ConstantExtension);
              image.Dispose();
            }
            break;
          }
          #endregion
          #region /SAVE
          case "/SAVE": {
            if (length - i < 1) {
              _ShowHelp();
              return (2);
            }
            var filename = arguments[i++];
            if (filename == null) {
              _ShowHelp();
              return (2);
            }

            if (!SaveHelper(filename, _currentImage))
              return (2);

            break;
          }
          #endregion
          #region /EXIT
          case "/EXIT": {
            return (0);
          }
          #endregion
          default: {
            _ShowHelp();
            return (1);
          }
        }
      }
      return (0);
    }

    /// <summary>
    /// Saves an image and adjust jpeg quality if saving to jpeg.
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// <param name="image">The image.</param>
    /// <returns><c>true</c> on success; otherwise, <c>false</c>.</returns>
    internal static bool SaveHelper(string filename, Image image) {
      Contract.Requires(filename != null);

      if (image == null) {
        MessageBox.Show(Resources.txNothingToSave, Resources.ttNothingToSave, MessageBoxButtons.OK, MessageBoxIcon.Stop);
        return false;
      }

      var extension = Path.GetExtension(filename);
      if (extension != null)
        extension = extension.ToUpperInvariant();

      if (extension != ".JPG" && extension != ".JPEG")
        image.Save(filename);
      else {
        var codecs = ImageCodecInfo.GetImageEncoders();
        codecs = codecs.Where(info => info != null && info.MimeType == "image/jpeg").ToArray();
        if (codecs.Length <= 0) {
          MessageBox.Show(Resources.ttNoJpegSupport, Resources.ttNoJpegSupport, MessageBoxButtons.OK, MessageBoxIcon.Stop);
          return false;
        }
        Contract.Assume(Encoder.Quality != null);
        image.Save(filename, codecs[0], new EncoderParameters {
          Param = new[] {
            new EncoderParameter(Encoder.Quality, (long)100)
          }
        });
      }
      return true;
    }

    /// <summary>
    /// Shows the CLI help.
    /// </summary>
    private static void _ShowHelp() {
      System.Reflection.Assembly objAssembly = System.Reflection.Assembly.GetExecutingAssembly();
      var lines = string.Join(
        Environment.NewLine,
        new[] {
          Path.GetFileName(objAssembly.Location)
            + " [/load <source>] [/resize <x>x<y> <method>[(<repeat>)]] [/save <target>] ... [/exit]",
          string.Empty,
          "  /load    - loads a file into the source buffer",
          "    <source> - the source file to resize",
          string.Empty,
          "  /save    - saves the image in the source buffer to a file",
          "    <target> - the target file to write",
          string.Empty,
          "  /resize  - resizes the image in the source buffer and stores the result back to the source buffer",
          "    <x>      - the final width in pixels for the target, 0 for auto",
          "    <y>      - the final height in pixels for the target, 0 for auto",
          "    <method> - the method to use",
          "    <repeat> - the number of repetitions using this method",
          string.Empty,
          "  /exit      - quits the program without showing the gui",
          string.Empty,
          string.Empty,
          "You can load and process multiple files at once by loading after saving again.",
          "ie. /load 1.bmp /resize 10x10 Pixel /save 1.jpg /load 2.bmp /resize 10x10 Pixel /save 2.jpg",
          string.Empty,
          "You can also save to multiple files by adding another save parameter.",
          "ie. /load 1.bmp /resize 10x10 Pixel /save 1.jpg /save 2.jpg",
          string.Empty,
          "Even preprocessing using multiple filters is possible by adding another resize parameter.",
          "ie. /load 1.bmp /resize 10x10 Pixel /resize 0x0 Scale2x /save 1.jpg",
          string.Empty,
          "Supported filter methods:"
        }.Concat(cImage.Filters.Select(f => "  " + f.Name)));

      Console.WriteLine(lines);
    }

  }
}

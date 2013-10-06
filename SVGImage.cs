namespace librsvgdotnet
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices;
    using System.Security;

    /// <summary>
    /// The SVGImage class allows a C# Bitmap to be rendered from a string containing SVG data
    /// </summary>
    public class SVGImage : IDisposable
    {
        private IntPtr _rsvgHandle = IntPtr.Zero;
        private static bool _initialized = false;
        
        /// <summary>
        /// Create a new SVGImage instance using a string containing SVG data
        /// </summary>
        /// <param name="data">The SVG data</param>
        public SVGImage(string data)
        {
            if (!_initialized)
            {
                g_type_init();
                rsvg_init();
                _initialized = true;
            }

            ReadFile(data);
        }

        /// <summary>
        /// Creates a C# Bitmap from the SVGImage
        /// </summary>
        /// <param name="w">The desired width</param>
        /// <param name="h">The desired height</param>
        /// <param name="stretch">If true, stretch the SVG image to fit the width and height exactly</param>
        /// <returns>A new Bitmap containing the SVG image</returns>
        public Bitmap Image(int w, int h, bool stretch)
        {
            RsvgDimensionData dim = new RsvgDimensionData();

            int dw = 0;
            int dh = 0;

            if (_rsvgHandle == IntPtr.Zero)
            {
                return null;
            }

            rsvg_handle_get_dimensions(_rsvgHandle, ref dim);

            double scaleX = w / ((double)dim.width);
            double scaleY = h / ((double)dim.height);

            if (stretch)
            {
                dw = w;
                dh = h;
            }
            else
            {
                double fixedScale = (scaleX < scaleY ? scaleX : scaleY);
                double fixedWidth = dim.width * fixedScale;
                double fixedHeight = dim.height * fixedScale;
                scaleX = fixedScale;
                scaleY = fixedScale;
                dw = (int)fixedWidth;
                dh = (int)fixedHeight;
            }

            //// Initialize the gdk_pixbuf
            IntPtr pixbuf = gdk_pixbuf_new(ColorSpace.Rgb, true, 8, dw, dh);
            int stride = gdk_pixbuf_get_rowstride(pixbuf);
            int width = dw;
            int height = dh;

            IntPtr pixels = gdk_pixbuf_get_pixels(pixbuf);
            byte[] src = new byte[stride * height];
            Marshal.Copy(src, 0, pixels, src.Length);

            //// Create the cairo surface
            IntPtr surface = cairo_image_surface_create_for_data(gdk_pixbuf_get_pixels(pixbuf), 0, width, height, stride);
            IntPtr cairo = cairo_create(surface);

            //// Set the scale and render the image
            cairo_scale(cairo, scaleX, scaleY);
            rsvg_handle_render_cairo(_rsvgHandle, cairo);

            //// Destroy the cairo surface
            cairo_destroy(cairo);
            cairo_surface_destroy(surface);

            //// Copy the gdk_pixbuf into a bitmap
            Bitmap bitmap = new Bitmap(width, height);
            byte[] temp = new byte[stride * height];
            Marshal.Copy(pixels, temp, 0, temp.Length);

            BitmapData bd = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            if (bd.Stride == stride)
            {
                Marshal.Copy(temp, 0, bd.Scan0, temp.Length);
            }
            else
            {
                for (int y = 0; y < height; ++y)
                {
                    Marshal.Copy(temp, y * stride, new IntPtr(bd.Scan0.ToInt64() + y * bd.Stride), width * 4);
                }
            }

            bitmap.UnlockBits(bd);

            //// Release the gdk_pixbuf
            g_object_unref(pixbuf);

            return bitmap;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public void ReadFile(string data)
        {
            IntPtr error = IntPtr.Zero;
            IntPtr handle = IntPtr.Zero;

            try
            {
                handle = rsvg_handle_new_from_data(data, data.Length - 1, out error);
            }
            catch (Exception e)
            {
                throw e;
            }

            if (handle == IntPtr.Zero)
            {
                if (error != IntPtr.Zero)
                {
                    GError errorstruct = (GError)Marshal.PtrToStructure(error, typeof(GError));
                    throw new Exception(errorstruct.message);
                }
                throw new Exception();
            }
            else
            {
                _rsvgHandle = handle;
            }
        }

        /// <summary>
        /// Free the memory associated with the SVGImage
        /// </summary>
        public void Dispose()
        {
            if (_rsvgHandle != IntPtr.Zero)
            {
                rsvg_handle_free(_rsvgHandle);
            }
            _rsvgHandle = IntPtr.Zero;
        }

        #region P/Invoke

        private enum ColorSpace { Rgb };

        [StructLayout(LayoutKind.Sequential)]
        struct RsvgDimensionData
        {
            public int width;
            public int height;
            public double em;
            public double ex;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct GError
        {
            int domain;
            int code;
            public string message;
        }

        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void rsvg_init();
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        static extern IntPtr rsvg_handle_new_from_data([MarshalAs(UnmanagedType.LPStr)]string data, int data_len, out IntPtr error);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        static extern IntPtr rsvg_handle_new_from_file([MarshalAs(UnmanagedType.LPStr)]string file_name, out IntPtr error);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void rsvg_handle_free(IntPtr handle);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool rsvg_handle_render_cairo(IntPtr handle, IntPtr cairo);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void rsvg_handle_get_dimensions(IntPtr handle, ref RsvgDimensionData dimension_data);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr cairo_create(IntPtr target);
        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void cairo_destroy(IntPtr cairo);
        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void cairo_surface_destroy(IntPtr cairo);
        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void cairo_scale(IntPtr cairo, double w, double h);
        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr cairo_image_surface_create_for_data(IntPtr data, int format, int width, int height, int stride);

        [DllImport("libgobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void g_type_init();
        [DllImport("libgobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void g_object_unref(IntPtr obj);

        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr gdk_pixbuf_get_pixels(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int gdk_pixbuf_get_rowstride(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr gdk_pixbuf_new(ColorSpace colorspace, bool has_alpha, int bits_per_sample, int width, int height);

        #endregion
    }
}

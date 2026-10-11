using System;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;
using UnityObject = UnityEngine.Object;

namespace MxFramework.Camera.Unity
{
    public readonly struct MxUiPreviewCameraSlotResult
    {
        public MxUiPreviewCameraSlotResult(
            bool success,
            string code,
            string message,
            int width,
            int height,
            bool textureAssigned)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Width = width;
            Height = height;
            TextureAssigned = textureAssigned;
        }

        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }
        public int Width { get; }
        public int Height { get; }
        public bool TextureAssigned { get; }

        public static MxUiPreviewCameraSlotResult Ok(int width, int height, bool textureAssigned)
        {
            return new MxUiPreviewCameraSlotResult(true, string.Empty, string.Empty, width, height, textureAssigned);
        }

        public static MxUiPreviewCameraSlotResult Failed(string code, string message)
        {
            return new MxUiPreviewCameraSlotResult(false, code, message, 0, 0, false);
        }
    }

    public sealed class MxUiPreviewTextureHandle : IDisposable
    {
        private RenderTexture _texture;
        private int _width;
        private int _height;
        private int _depthBits;
        private RenderTextureFormat _format;

        public RenderTexture Texture => _texture;
        public int Width => _width;
        public int Height => _height;
        public int DepthBits => _depthBits;
        public bool HasTexture => _texture != null;

        public MxUiPreviewCameraSlotResult EnsureTexture(
            int width,
            int height,
            int depthBits = 16,
            RenderTextureFormat format = RenderTextureFormat.Default)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            depthBits = Math.Max(0, depthBits);

            if (_texture != null
                && _width == width
                && _height == height
                && _depthBits == depthBits
                && _format == format)
            {
                return MxUiPreviewCameraSlotResult.Ok(_width, _height, true);
            }

            Release();
            _width = width;
            _height = height;
            _depthBits = depthBits;
            _format = format;
            _texture = new RenderTexture(width, height, depthBits, format)
            {
                name = "MxUiPreviewTexture",
                useMipMap = false,
                autoGenerateMips = false
            };
            _texture.Create();
            return MxUiPreviewCameraSlotResult.Ok(_width, _height, true);
        }

        public void Release()
        {
            if (_texture == null)
                return;

            _texture.Release();
            DestroyObject(_texture);
            _texture = null;
            _width = 0;
            _height = 0;
            _depthBits = 0;
            _format = RenderTextureFormat.Default;
        }

        public void Dispose()
        {
            Release();
        }

        private static void DestroyObject(UnityObject obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                UnityObject.Destroy(obj);
            else
                UnityObject.DestroyImmediate(obj);
        }
    }

    public sealed class MxUiPreviewCameraSlot : IDisposable
    {
        public const string MissingCameraCode = "CAM_UI_PREVIEW_CAMERA_MISSING";
        public const string TextureMissingCode = "CAM_UI_PREVIEW_TEXTURE_MISSING";

        private readonly MxUiPreviewTextureHandle _textureHandle;
        private UnityCamera _camera;

        public MxUiPreviewCameraSlot(UnityCamera camera)
            : this(camera, new MxUiPreviewTextureHandle())
        {
        }

        public MxUiPreviewCameraSlot(UnityCamera camera, MxUiPreviewTextureHandle textureHandle)
        {
            _camera = camera;
            _textureHandle = textureHandle ?? new MxUiPreviewTextureHandle();
        }

        public UnityCamera Camera => _camera;
        public RenderTexture TargetTexture => _textureHandle.Texture;
        public bool HasTexture => _textureHandle.HasTexture;
        public int Width => _textureHandle.Width;
        public int Height => _textureHandle.Height;

        public void SetCamera(UnityCamera camera)
        {
            if (_camera == camera)
                return;

            if (_camera != null && _camera.targetTexture == _textureHandle.Texture)
                _camera.targetTexture = null;

            _camera = camera;
            if (_camera != null && _textureHandle.Texture != null)
                _camera.targetTexture = _textureHandle.Texture;
        }

        public MxUiPreviewCameraSlotResult EnsureTexture(
            int width,
            int height,
            int depthBits = 16,
            RenderTextureFormat format = RenderTextureFormat.Default)
        {
            if (_camera == null)
                return MxUiPreviewCameraSlotResult.Failed(MissingCameraCode, "Preview camera is missing.");

            if (_camera.targetTexture == _textureHandle.Texture)
                _camera.targetTexture = null;

            MxUiPreviewCameraSlotResult result = _textureHandle.EnsureTexture(width, height, depthBits, format);
            if (!result.Success || _textureHandle.Texture == null)
                return MxUiPreviewCameraSlotResult.Failed(TextureMissingCode, "Preview RenderTexture is missing.");

            _camera.targetTexture = _textureHandle.Texture;
            return MxUiPreviewCameraSlotResult.Ok(_textureHandle.Width, _textureHandle.Height, _camera.targetTexture != null);
        }

        public MxUiPreviewCameraSlotResult ReleaseTexture()
        {
            if (_camera != null && _camera.targetTexture == _textureHandle.Texture)
                _camera.targetTexture = null;

            _textureHandle.Release();
            return MxUiPreviewCameraSlotResult.Ok(0, 0, false);
        }

        public MxUiPreviewCameraSlotResult CaptureState()
        {
            if (_camera == null)
                return MxUiPreviewCameraSlotResult.Failed(MissingCameraCode, "Preview camera is missing.");

            if (_textureHandle.Texture == null || _camera.targetTexture == null)
                return MxUiPreviewCameraSlotResult.Failed(TextureMissingCode, "Preview RenderTexture is missing.");

            return MxUiPreviewCameraSlotResult.Ok(_textureHandle.Width, _textureHandle.Height, true);
        }

        public void Dispose()
        {
            ReleaseTexture();
        }
    }
}

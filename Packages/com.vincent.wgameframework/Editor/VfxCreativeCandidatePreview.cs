using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Captures deterministic static evidence for generated ParticleSystem and LineRenderer Prefabs.</summary>
    /// <remarks>Editor-only and AllocByDesign. Uses PreviewRenderUtility and writes evidence only under Temp.</remarks>
    internal sealed class VfxDeterministicPreviewCapture : IVfxPreviewCapture
    {
        public VfxPreviewCaptureResult Capture(VfxPreviewCaptureRequest request)
        {
            if (request == null)
                return Failure("VFX preview capture request is required.");
            string prefabPath = VfxCandidateSandbox.NormalizeAssetPath(request.PrefabAssetPath);
            if (!VfxCandidateSandbox.IsCandidateRoot(prefabPath))
                return Failure("VFX preview Prefab must be inside the candidate sandbox: " + prefabPath);
            if (!VfxCandidateSandbox.TryGetSessionRoot(request.SessionId, out string sessionRoot, out string sessionError)
                || !VfxCandidateSandbox.IsWithinSessionRoot(prefabPath, sessionRoot))
                return Failure(string.IsNullOrEmpty(sessionError) ? "VFX preview Prefab does not belong to the requested session." : sessionError);
            if (!IsSafeEvidenceStem(request.CandidateId) || !IsSafeEvidenceStem(request.CaptureId)
                || !IsSafeEvidenceStem(request.ViewId) || !IsSafeEvidenceStem(request.BackgroundId) || !IsSafeEvidenceStem(request.TimeSliceId))
                return Failure("VFX preview candidate/capture/view/background/time identifiers must be non-empty stable path segments.");
            if (!string.Equals(request.ViewId, "Gameplay", StringComparison.Ordinal)
                || !string.Equals(request.BackgroundId, "DarkNeutral", StringComparison.Ordinal))
                return Failure("This preview adapter currently supports only Gameplay/DarkNeutral; unsupported evidence dimensions fail closed.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return Failure("VFX preview currently supports generated Prefabs; a Graph clone needs a certified preview wrapper: " + prefabPath);

            int width = Mathf.Clamp(request.Width, 64, 1024);
            int height = Mathf.Clamp(request.Height, 64, 1024);
            var preview = new PreviewRenderUtility();
            Texture2D texture = null;
            try
            {
                preview.BeginStaticPreview(new Rect(0f, 0f, width, height));
                GameObject instance = preview.InstantiatePrefabInScene(prefab);
                ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particles.Length; i++)
                {
                    particles[i].useAutoRandomSeed = false;
                    particles[i].randomSeed = unchecked(request.FixedSeed + (uint)i * 7919u);
                    particles[i].Simulate(Mathf.Max(0f, request.SampleTimeSeconds), true, true, true);
                }

                Bounds bounds = CalculateBounds(instance);
                float radius = Mathf.Max(0.35f, bounds.extents.magnitude);
                Vector3 direction = new Vector3(0.65f, 0.3f, -1f).normalized;
                preview.camera.transform.position = bounds.center + direction * radius * 3.2f;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.nearClipPlane = Mathf.Max(0.01f, radius * 0.01f);
                preview.camera.farClipPlane = radius * 12f + 10f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 0f);
                preview.cameraFieldOfView = 32f;
                preview.ambientColor = new Color(0.3f, 0.32f, 0.36f, 1f);
                if (preview.lights.Length > 0)
                {
                    preview.lights[0].intensity = 1.15f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                }
                preview.Render(true, true);
                texture = preview.EndStaticPreview();
                Color[] pixels = texture.GetPixels();
                CalculateMetrics(pixels, out float alphaCoverage, out float meanLuminance, out float peakLuminance);
                byte[] png = texture.EncodeToPNG();
                string outputPath = BuildOutputPath(
                    request.SessionId,
                    request.CandidateId,
                    request.CaptureId,
                    request.ViewId,
                    request.BackgroundId,
                    request.TimeSliceId);
                string outputDirectory = Path.GetDirectoryName(outputPath)?.Replace('\\', '/')
                    ?? throw new InvalidOperationException("VFX preview evidence directory is missing.");
                Directory.CreateDirectory(outputDirectory);
                if (!VfxPreviewEvidenceFileTransaction.Publish(outputPath, png, out string publishMessage))
                    return Failure(publishMessage);
                return new VfxPreviewCaptureResult
                {
                    Success = true,
                    Message = "Deterministic candidate preview captured. Evidence does not imply artistic approval.",
                    CandidateId = request.CandidateId,
                    CaptureId = request.CaptureId,
                    FixedSeed = request.FixedSeed,
                    ViewId = request.ViewId,
                    BackgroundId = request.BackgroundId,
                    TimeSliceId = request.TimeSliceId,
                    OutputPath = outputPath,
                    ContentHash = VfxCandidateSandbox.ComputeHash(png),
                    AlphaCoverage = alphaCoverage,
                    MeanLuminance = meanLuminance,
                    PeakLuminance = peakLuminance
                };
            }
            catch (Exception exception)
            {
                return Failure(exception.Message);
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
                preview.Cleanup();
            }
        }

        internal static string BuildOutputPath(
            string sessionId,
            string candidateId,
            string captureId,
            string viewId,
            string backgroundId,
            string timeSliceId)
        {
            if (!IsSafeEvidenceStem(sessionId)
                || !IsSafeEvidenceStem(candidateId)
                || !IsSafeEvidenceStem(captureId)
                || !IsSafeEvidenceStem(viewId)
                || !IsSafeEvidenceStem(backgroundId)
                || !IsSafeEvidenceStem(timeSliceId))
                throw new InvalidOperationException("VFX preview evidence identifiers must be safe path segments.");
            return "Temp/MxFramework/VfxCandidates/" + sessionId
                + "/" + candidateId
                + "/" + captureId
                + "/" + viewId
                + "/" + backgroundId
                + "/" + timeSliceId
                + "/preview.png";
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one * 2f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.extents.sqrMagnitude < 0.0001f)
                bounds.extents = Vector3.one;
            return bounds;
        }

        private static void CalculateMetrics(Color[] pixels, out float alphaCoverage, out float meanLuminance, out float peakLuminance)
        {
            int covered = 0;
            float luminanceSum = 0f;
            peakLuminance = 0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (pixel.a <= 0.01f)
                    continue;
                covered++;
                float luminance = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                luminanceSum += luminance;
                peakLuminance = Mathf.Max(peakLuminance, luminance);
            }
            alphaCoverage = pixels.Length > 0 ? covered / (float)pixels.Length : 0f;
            meanLuminance = covered > 0 ? luminanceSum / covered : 0f;
        }

        private static VfxPreviewCaptureResult Failure(string message)
        {
            return new VfxPreviewCaptureResult
            {
                Success = false,
                Message = message ?? string.Empty
            };
        }

        private static bool IsSafeEvidenceStem(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_' && character != '.')
                    return false;
            }
            return !string.Equals(value, ".", StringComparison.Ordinal) && !string.Equals(value, "..", StringComparison.Ordinal);
        }
    }

    internal static class VfxPreviewEvidenceFileTransaction
    {
        internal static bool Publish(
            string outputPath,
            byte[] contents,
            out string message,
            Action<string, byte[]> writer = null,
            Action<string, string, string> publisher = null)
        {
            string directory = Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException("VFX preview evidence directory is missing.");
            Directory.CreateDirectory(directory);
            string token = Guid.NewGuid().ToString("N");
            string temporaryPath = Path.Combine(directory, ".preview.png." + token + ".tmp");
            string backupPath = Path.Combine(directory, ".preview.png." + token + ".bak");
            bool hadExistingOutput = File.Exists(outputPath);
            byte[] originalContents = hadExistingOutput ? File.ReadAllBytes(outputPath) : null;
            string expectedHash = VfxCandidateSandbox.ComputeHash(contents);
            try
            {
                (writer ?? File.WriteAllBytes)(temporaryPath, contents);
                byte[] prepared = File.ReadAllBytes(temporaryPath);
                if (!prepared.SequenceEqual(contents)
                    || !string.Equals(VfxCandidateSandbox.ComputeHash(prepared), expectedHash, StringComparison.Ordinal))
                    throw new InvalidDataException("VFX preview temp bytes/hash did not match the encoded PNG.");
                (publisher ?? PublishFile)(temporaryPath, outputPath, backupPath);
                byte[] published = File.ReadAllBytes(outputPath);
                if (!published.SequenceEqual(contents)
                    || !string.Equals(VfxCandidateSandbox.ComputeHash(published), expectedHash, StringComparison.Ordinal))
                    throw new InvalidDataException("Published VFX preview bytes/hash did not match the encoded PNG.");
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    if (hadExistingOutput)
                    {
                        if (File.Exists(backupPath) && File.Exists(outputPath))
                            File.Replace(backupPath, outputPath, null, true);
                        else
                            File.WriteAllBytes(outputPath, originalContents);
                    }
                    else if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                    message = "VFX preview evidence publish failed and preserved the prior file: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "VFX preview evidence publish failed: " + exception.Message
                        + " Evidence rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
        }

        private static void PublishFile(string temporaryPath, string outputPath, string backupPath)
        {
            if (File.Exists(outputPath)) File.Replace(temporaryPath, outputPath, backupPath, true);
            else File.Move(temporaryPath, outputPath);
        }
    }

    /// <summary>Scores deterministic preview evidence with conservative machine-checkable visual gates.</summary>
    /// <remarks>Editor-only and AllocByDesign. A passing score remains ReviewNeeded and never grants Ready.</remarks>
    internal sealed class VfxDefaultVisualEvaluator : IVfxVisualEvaluator
    {
        public VfxVisualEvaluationResult Evaluate(VfxCandidatePlan plan, VfxPreviewCaptureResult capture)
        {
            var result = new VfxVisualEvaluationResult();
            if (plan == null)
                result.Diagnostics.Add("Candidate plan is missing.");
            if (capture == null || !capture.Success)
            {
                result.Diagnostics.Add("Deterministic preview capture failed: " + (capture?.Message ?? "missing result"));
                return result;
            }
            if (capture.AlphaCoverage < 0.001f)
                result.Diagnostics.Add("Visible coverage is effectively empty.");
            if (capture.AlphaCoverage > 0.9f)
                result.Diagnostics.Add("Visible coverage fills almost the entire frame; framing or overdraw needs review.");
            if (capture.PeakLuminance < 0.02f)
                result.Diagnostics.Add("Peak luminance is too low to establish a readable effect silhouette.");
            if (capture.MeanLuminance > 0.98f)
                result.Diagnostics.Add("Mean luminance is near clipping and needs exposure review.");

            result.PassedHardGates = result.Diagnostics.Count == 0;
            int coverageScore = Mathf.RoundToInt(Mathf.Clamp01(capture.AlphaCoverage / 0.18f) * 35f);
            int peakScore = Mathf.RoundToInt(Mathf.Clamp01(capture.PeakLuminance) * 35f);
            int meanScore = Mathf.RoundToInt(Mathf.Clamp01(capture.MeanLuminance / 0.5f) * 30f);
            result.Score = result.PassedHardGates ? Mathf.Clamp(coverageScore + peakScore + meanScore, 0, 100) : 0;
            if (result.PassedHardGates)
                result.Diagnostics.Add("Machine visual gates passed; composition, timing, readability, style, and budget still require human review.");
            return result;
        }
    }
}

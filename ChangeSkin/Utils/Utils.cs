using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;
using static ChangeSkinMP.ChangeBody;
using Random = System.Random;

namespace ChangeSkinMP
{
    internal static class Utils
    {
        public static Texture2D LoadTexture(string path)
        {
            Texture2D newTexture = new Texture2D(2, 2);
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                newTexture.LoadImage(bytes);
                newTexture.filterMode = FilterMode.Point;
                newTexture.name = Path.GetFileNameWithoutExtension(path);
            }
            catch
            {
                Plugin.Logger.LogWarning($"Failed to load texture at: {path}");
                throw;
            }
            return newTexture;
        }

        public static Sprite LoadSprite(string path)
        {
            Sprite outSprite = new();
            try
            {
                Texture2D tex = LoadTexture(path);
                outSprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    8,
                    0,
                    SpriteMeshType.Tight
                );
                outSprite.name = Path.GetFileNameWithoutExtension(path);
            }
            catch
            {
                Plugin.Logger.LogWarning($"Failed to load sprite at: {path}");
                throw;
            }
            return outSprite;
        }

        public static Sprite LoadSprite(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            if (!tex.LoadImage(bytes))
                return null;

            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                8,
                0
            );
        }

        public static void SerializeSprite(NetDataWriter writer, Sprite sprite)
        {
            Texture2D tex = sprite.texture;

            // Если текстура не читаема — копируем через RenderTexture
            if (!tex.isReadable)
            {
                var rt = RenderTexture.GetTemporary(tex.width, tex.height);
                Graphics.Blit(tex, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var readable = new Texture2D(tex.width, tex.height);
                readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                readable.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                tex = readable;
            }

            byte[] png = tex.EncodeToPNG();
            writer.Put(png.Length);
            writer.Put(png);

            // Сохраняем rect и pivot для точного восстановления спрайта
            Rect r = sprite.rect;
            writer.Put(r.x);
            writer.Put(r.y);
            writer.Put(r.width);
            writer.Put(r.height);

            Vector2 pivot = sprite.pivot;
            writer.Put(pivot.x);
            writer.Put(pivot.y);

            writer.Put(sprite.pixelsPerUnit);
        }

        public static Sprite DeserializeSprite(NetDataReader reader)
        {
            int len = reader.GetInt();
            byte[] png = new byte[len];
            reader.GetBytes(png, len);

            var tex = new Texture2D(2, 2);
            tex.LoadImage(png); // автоматически ресайзит

            float rx = reader.GetFloat(),
                ry = reader.GetFloat();
            float rw = reader.GetFloat(),
                rh = reader.GetFloat();
            float px = reader.GetFloat(),
                py = reader.GetFloat();
            float ppu = reader.GetFloat();

            return Sprite.Create(
                tex,
                new Rect(rx, ry, rw, rh),
                new Vector2(px / rw, py / rh), // нормализованный pivot
                ppu
            );
        }

        public static string GenerateRandomString(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(
                Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray()
            );
        }

        internal static string UploadLocalSkin(SkinInfo skinInfo)
        {
            string workPath = Path.GetTempPath() + "/ChangeSkin/zips/local";
            string filePath = workPath + $"/{skinInfo.name}.zip";
            Uri uri = new(Plugin.ModConfig.uploadApiUrl);
            byte[] fileBytes = File.ReadAllBytes(filePath);
            using (HttpClient client = new HttpClient())
            using (var formData = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                formData.Add(fileContent, "file", Path.GetFileName(filePath));
                HttpResponseMessage response = client
                    .PostAsync(Plugin.ModConfig.uploadApiUrl, formData)
                    .Result;

                if (response.StatusCode == HttpStatusCode.Created)
                {
                    string result = response.Headers.GetValues("Location").FirstOrDefault();
                    Plugin.Logger.LogInfo(result);
                    return result;
                }
                else
                {
                    Plugin.Logger.LogWarning(
                        "Bad response from server: " + response.Content.ReadAsStringAsync().Result
                    );
                }
            }
            return null;
        }

        // public static async Task<AudioClip> LoadAudio(string path)
        // {
        // AudioClip clip = null;
        // using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.WAV))
        // {
        // uwr.SendWebRequest();

        // try
        // {
        //     while (!uwr.isDone) await Task.Delay(5);

        //     if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError) Debug.Log($"{uwr.error}");
        //     else
        //     {
        //         clip = DownloadHandlerAudioClip.GetContent(uwr);
        //     }
        // }
        // catch (Exception err)
        // {
        //     Debug.Log($"{err.Message}, {err.StackTrace}");
        // }
        // }

        // return clip;
        // }
    }
}

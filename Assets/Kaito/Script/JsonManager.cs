using System;
using System.IO;
using UnityEngine;


    public static class JsonManager
    {
        /// <summary>
        /// オブジェクトをJSONへシリアライズして保存する
        /// </summary>
        /// <typeparam name="T">保存する型</typeparam>
        /// <param name="path">保存先ファイルパス</param>
        /// <param name="data">保存データ</param>
        /// <param name="prettyPrint">整形出力するか</param>
        /// <exception cref="ArgumentNullException">パスがnullの場合にスルーされます。</exception>
        public static void DefaultSave<T>(string path, T data, bool prettyPrint = false)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (data == null) throw new ArgumentNullException(nameof(data));
            CreateDirectory(path);
            string json = JsonUtility.ToJson(data, prettyPrint);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// 未暗号化JSONファイルを読込み、オブジェクトへ変換させる
        /// </summary>
        /// <typeparam name="T">所定の型</typeparam>
        /// <param name="path">ファイルのフルパス</param>
        /// <returns></returns>
        public static T DefaultLoad<T>(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("保存ファイルが存在しません。", path);
            }
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<T>(json);
        }

        /// <summary>
        /// 保存先ディレクトリを作成します。
        /// </summary>
        private static void CreateDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

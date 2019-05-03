﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static LiteDB.Constants;

namespace LiteDB.Engine
{
    internal class SysFileJson : SystemCollection
    {
        public SysFileJson() : base("$file_json")
        {
        }

        public override bool IsFunction => true;

        public override IEnumerable<BsonDocument> Input(LiteEngine engine, BsonValue options)
        {
            if (options == null || (!options.IsString && !options.IsDocument)) throw new LiteException(0, $"Collection ${this.Name} requires a string/object parameter");

            var filename = GetOption(options, true, "filename", null).AsString ?? throw new LiteException(0, $"Collection ${this.Name} requires string as 'filename' or a document field 'filename'");

            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = new StreamReader(fs))
                {
                    var json = new JsonReader(reader);

                    var source = json.DeserializeArray()
                        .Select(x => x.AsDocument);

                    // read documents inside file and return one-by-one
                    foreach (var doc in source)
                    {
                        yield return doc;
                    }
                }
            }
        }

        public override int Output(IEnumerable<BsonDocument> source, BsonValue options)
        {
            if (options == null || (!options.IsString && !options.IsDocument)) throw new LiteException(0, "Collection $file_json requires a string/object parameter");

            var filename = GetOption(options, true, "filename", null).AsString ?? throw new LiteException(0, "Collection $file_json requires string as 'filename' or a document field 'filename'");
            var pretty = GetOption(options, false, "pretty", false).AsBoolean;
            var indent = GetOption(options, false, "indent", 4).AsInt32;
            var encoding = GetOption(options, false, "encoding", "utf-8").AsString;
            var overwritten = GetOption(options, false, "overwritten", false).AsBoolean;

            var index = 0;
            FileStream fs = null;
            StreamWriter writer = null;
            JsonWriter json = null;

            try
            {
                foreach (var doc in source)
                {
                    if (index++ == 0)
                    {
                        fs = new FileStream(filename, overwritten ? FileMode.OpenOrCreate : FileMode.CreateNew);
                        writer = new StreamWriter(fs, Encoding.GetEncoding(encoding));
                        json = new JsonWriter(writer)
                        {
                            Pretty = pretty,
                            Indent = indent
                        };

                        writer.WriteLine("[");
                    }
                    else
                    {
                        writer.WriteLine(",");
                    }

                    json.Serialize(doc);
                }

                if (index > 0)
                {
                    writer.WriteLine();
                    writer.Write("]");
                    writer.Flush();
                }
            }
            finally
            {
                if (writer != null) writer.Dispose();
                if (fs != null) fs.Dispose();
            }

            return index;
        }
    }
}
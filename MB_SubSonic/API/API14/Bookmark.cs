// ------------------------------------------------------------------------------
//  <auto-generated>
//    Generated by Xsd2Code++. Version 4.2.0.31
//  </auto-generated>
// ------------------------------------------------------------------------------
#pragma warning disable
namespace MusicBeePlugin.API14
{
using System;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Collections;
using System.Xml.Schema;
using System.ComponentModel;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections.Generic;

[DebuggerStepThrough]
public partial class Bookmark
{
    
        public Child Entry { get; set; }
        public long Position { get; set; }
        public string Username { get; set; }
        public string Comment { get; set; }
        public System.DateTime Created { get; set; }
        public System.DateTime Changed { get; set; }
    
    public Bookmark()
    {
        Entry = new Child();
    }
    
    #region Serialize/Deserialize
    /// <summary>
    /// Serializes current Bookmark object into an json string
    /// </summary>
    public virtual string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    
    /// <summary>
    /// Deserializes workflow markup into an Bookmark object
    /// </summary>
    /// <param name="input">string workflow markup to deserialize</param>
    /// <param name="obj">Output Bookmark object</param>
    /// <param name="exception">output Exception value if deserialize failed</param>
    /// <returns>true if this Serializer can deserialize the object; otherwise, false</returns>
    public static bool Deserialize(string input, out Bookmark obj, out Exception exception)
    {
        exception = null;
        obj = default(Bookmark);
        try
        {
            obj = Deserialize(input);
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }
    
    public static bool Deserialize(string input, out Bookmark obj)
    {
        Exception exception = null;
        return Deserialize(input, out obj, out exception);
    }
    
    public static Bookmark Deserialize(string input)
    {
        return JsonConvert.DeserializeObject<Bookmark>(input);
    }
    #endregion
    
    public virtual void SaveToFile(string fileName)
    {
        StreamWriter streamWriter = null;
        try
        {
            string xmlString = Serialize();
            streamWriter = new StreamWriter(fileName, false, System.Text.Encoding.UTF8);
            streamWriter.WriteLine(xmlString);
            streamWriter.Close();
        }
        finally
        {
            if ((streamWriter != null))
            {
                streamWriter.Dispose();
            }
        }
    }
    
    public static Bookmark LoadFromFile(string fileName)
    {
        FileStream file = null;
        StreamReader sr = null;
        try
        {
            file = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            sr = new StreamReader(file);
            string xmlString = sr.ReadToEnd();
            sr.Close();
            file.Close();
            return Deserialize(xmlString);
        }
        finally
        {
            if ((file != null))
            {
                file.Dispose();
            }
            if ((sr != null))
            {
                sr.Dispose();
            }
        }
    }
}
}
#pragma warning restore

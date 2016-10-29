// ------------------------------------------------------------------------------
//  <auto-generated>
//    Generated by Xsd2Code++. Version 4.2.0.31
//  </auto-generated>
// ------------------------------------------------------------------------------
#pragma warning disable
namespace MusicBeePlugin.API13
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
public partial class JukeboxPlaylist : JukeboxStatus
{
    
        public List<Child> Entry { get; set; }
    
    public JukeboxPlaylist()
    {
        Entry = new List<Child>();
    }
    
    #region Serialize/Deserialize
    /// <summary>
    /// Serializes current JukeboxPlaylist object into an json string
    /// </summary>
    public virtual string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    
    /// <summary>
    /// Deserializes workflow markup into an JukeboxPlaylist object
    /// </summary>
    /// <param name="input">string workflow markup to deserialize</param>
    /// <param name="obj">Output JukeboxPlaylist object</param>
    /// <param name="exception">output Exception value if deserialize failed</param>
    /// <returns>true if this Serializer can deserialize the object; otherwise, false</returns>
    public static bool Deserialize(string input, out JukeboxPlaylist obj, out Exception exception)
    {
        exception = null;
        obj = default(JukeboxPlaylist);
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
    
    public static bool Deserialize(string input, out JukeboxPlaylist obj)
    {
        Exception exception = null;
        return Deserialize(input, out obj, out exception);
    }
    
    public new static JukeboxPlaylist Deserialize(string input)
    {
        return JsonConvert.DeserializeObject<JukeboxPlaylist>(input);
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
    
    public new static JukeboxPlaylist LoadFromFile(string fileName)
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

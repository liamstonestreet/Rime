using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

sealed class MediaMetadataStore {
 readonly object gate=new object();readonly Func<DateTime> now;string url,title,album,playlist,albumUrl,playlistUrl,artistUrl;bool? shuffle;DateTime updated;
 public MediaMetadataStore(Func<DateTime> now){this.now=now;}
 public string Current(string expected){lock(gate)return now()-updated<TimeSpan.FromSeconds(45)&&String.Equals(expected,title,StringComparison.OrdinalIgnoreCase)?url:null;}
 public string[] Context(string expected){lock(gate)return Current(expected)==null?new[]{"",""}:new[]{album??"",playlist??""};}
 public MediaLinks Links(string expected){lock(gate)return Current(expected)==null?new MediaLinks(null,null,null):new MediaLinks(url,playlistUrl,albumUrl,artistUrl);}
 public bool? Shuffle(string expected){lock(gate)return Current(expected)==null?(bool?)null:shuffle;}
 internal static string ContextUrl(string value,string kind){Uri parsed;return Uri.TryCreate(value,UriKind.Absolute,out parsed)&&parsed.Scheme=="https"&&parsed.Host=="open.spotify.com"&&System.Text.RegularExpressions.Regex.IsMatch(parsed.AbsolutePath,"^/"+kind+"/[A-Za-z0-9]+$")?parsed.GetLeftPart(UriPartial.Path):null;}
 public string Snapshot(){lock(gate)return new JavaScriptSerializer().Serialize(new{url,title,album,playlist,albumUrl,playlistUrl,artistUrl,shuffle,ageSeconds=(now()-updated).TotalSeconds});}
 public string Receive(string payload){var data=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(payload);Uri parsed;
  if(data==null||!data.ContainsKey("url")||!data.ContainsKey("title")||!Uri.TryCreate(data["url"],UriKind.Absolute,out parsed)||parsed.Scheme!="https"||parsed.Host!="open.spotify.com"||!parsed.AbsolutePath.StartsWith("/track/",StringComparison.Ordinal)&&!parsed.AbsolutePath.StartsWith("/episode/",StringComparison.Ordinal))return "{\"ok\":false}";
  lock(gate){url=parsed.AbsoluteUri;title=data["title"];album=data.ContainsKey("album")?data["album"]:"";playlist=data.ContainsKey("playlist")?data["playlist"]:"";albumUrl=ContextUrl(data.ContainsKey("albumUrl")?data["albumUrl"]:null,"album");playlistUrl=ContextUrl(data.ContainsKey("playlistUrl")?data["playlistUrl"]:null,"playlist");artistUrl=ContextUrl(data.ContainsKey("artistUrl")?data["artistUrl"]:null,"artist");bool parsedShuffle;shuffle=data.ContainsKey("shuffle")&&Boolean.TryParse(data["shuffle"],out parsedShuffle)?(bool?)parsedShuffle:null;updated=now();}return "{\"ok\":true}";
 }
}

sealed class MediaLinks {
 public readonly string Song,Playlist,Album,Artist;
 public MediaLinks(string song,string playlist,string album,string artist=null){Song=song;Playlist=playlist;Album=album;Artist=artist;}
 public string Combined {get{return String.Join(" | ",new[]{"Song: "+(Song??"NA"),"Artist: "+(Artist??"NA"),"Playlist: "+(Playlist??"NA"),"Album: "+(Album??"NA")});}}
 public string Component(int index){return index==0?Combined:index==1?Song:index==2?Playlist:index==4?Artist:Album;}
}

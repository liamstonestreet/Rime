using System;
sealed class NotificationItem {
 public string Id {get;set;}public string Provider {get;set;}public string Source {get;set;}public string Title {get;set;}public string Body {get;set;}public string AppId {get;set;}public uint NativeId {get;set;}public DateTime CreatedUtc {get;set;}
 public string AppIcon {get;set;}
 public bool HasIcon {get{return !String.IsNullOrEmpty(AppIcon);}}
 public string Initials {get{var words=(Title??Source??"?").Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);return words.Length==0?"?":(words[0].Substring(0,1)+(words.Length>1?words[words.Length-1].Substring(0,1):"")).ToUpperInvariant();}}
 public bool Read {get;set;}public bool Pinned {get;set;}public bool Archived {get;set;}public bool NativeActive {get;set;}
 public string TimeLabel {get{return CreatedUtc.ToLocalTime().ToString("MMM d, h:mm tt");}}public string ReadLabel {get{return Read?"Unread":"Read";}}public string PinLabel {get{return Pinned?"Unpin":"Pin";}}
 public bool CanOpen {get{return !String.IsNullOrEmpty(AppId)&&AppId.Length<=256&&System.Text.RegularExpressions.Regex.IsMatch(AppId,@"^[A-Za-z0-9_.!{}-]+$");}}
 public NotificationItem Copy(){return (NotificationItem)MemberwiseClone();}
}

using System;
using System.Web.Script.Serialization;

// Font bytes have their own cache/channel; ordinary tint or motion edits never
// rebuild them. Delivery order is serialized here; transport remains MediaBridge.
sealed class AppearancePublisher {
 readonly IThemeHost host;readonly object gate=new object();string fontKey,fontPayload,sentFont,sent;
 public AppearancePublisher(IThemeHost host){this.host=host;}
 public string FontSnapshot(AppearanceSettings settings){lock(gate){string family=settings.fontFamily??"Rubik",key=family+"|"+settings.fontRevision;
  if(key!=fontKey){fontPayload=new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Serialize(new{font=new{family,revision=settings.fontRevision,data=host.FontData(family)}});fontKey=key;}return fontPayload;
 }}
 public void Publish(Func<AppearanceSettings> settings,Func<string> snapshot){lock(gate){var font=FontSnapshot(settings());if(font!=sentFont){host.Broadcast(font);sentFont=font;}var current=snapshot();if(current!=sent){host.Broadcast(current);sent=current;}}}
}

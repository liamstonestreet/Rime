using System;
static class StereoBalance {
 public static float Read(float left,float right){float maximum=Math.Max(left,right);return maximum>0?Math.Max(-1,Math.Min(1,(right-left)/maximum)):0;}
 // Endpoint channels use the live endpoint level, not a potentially stale UI slider value.
 public static float Apply(float pan,Func<float> master,Action<int,float> set,Func<int,float> get){
  pan=Math.Max(-1,Math.Min(1,pan));float level=Math.Max(0,Math.Min(1,master()));
  set(0,level*(pan>0?1-pan:1));set(1,level*(pan<0?1+pan:1));return Read(get(0),get(1));
 }
}

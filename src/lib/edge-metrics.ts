// Host HWND dimensions may round up at fractional DPI while WebView clips to
// the drawable physical pixels. Keep the entire edge inside that drawable strip.
export function edgeMetrics(cssHeight:number,scale:number,reportedHeight?:number,radius=0){
 const drawable=Math.floor(cssHeight*scale+1e-6);
 const height=Math.min(reportedHeight??drawable,drawable);
 const offset=cssHeight-height/scale,line=Math.floor(3*scale+1e-6)/scale;
 // A fractional offscreen layer height gets resampled by Chromium's transform.
 // Keep its extent integral without changing the actual corner radius.
 const sourceHeight=Math.ceil((line+radius+20)*scale-1e-6);
 return {height,offset,line,sourceHeight,bottom:offset+line-sourceHeight/scale};
}

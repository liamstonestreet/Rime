export const defaultLeft = ['notifications','workspaces','direction','search','rules'];
export const defaultRight = ['quickActions','volume','spotify','helium','tray','memory','cpu','vpn','power'];
export const widgetKeys = [...defaultLeft,...defaultRight];
export interface BarLayout {left:string[];right:string[]}
export function normalizeLayout(value:unknown):BarLayout {
 const raw=value as Partial<BarLayout>|undefined,seen=new Set<string>();
 const lane=(items:unknown)=>Array.isArray(items)?items.filter((key):key is string=>typeof key==='string'&&widgetKeys.includes(key)&&!seen.has(key)&&!!seen.add(key)):[];
 const left=lane(raw?.left),right=lane(raw?.right);
 if(!seen.has('notifications')){left.unshift('notifications');seen.add('notifications');}
 if(!seen.has('quickActions')){right.unshift('quickActions');seen.add('quickActions');}
 for(const key of defaultLeft)if(!seen.has(key)){left.push(key);seen.add(key);}
 for(const key of defaultRight)if(!seen.has(key)){right.push(key);seen.add(key);}
 return {left,right};
}

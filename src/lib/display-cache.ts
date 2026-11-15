import type {Appearance} from './appearance';
// Display-only cache: never restore process state, actions, or old transition clocks.
export function savedAppearance(value:Appearance) {
 const {revision,transition,...appearance}=value;
 return appearance;
}
export function readDisplay(key:string):any {
 try {return JSON.parse(localStorage.getItem('nord-display-'+key)||'null');} catch {return null;}
}
export function saveDisplay(key:string,value:unknown) {
 try {localStorage.setItem('nord-display-'+key,JSON.stringify(value));} catch {/* Live configuration always remains authoritative. */}
}

import { ITileSet } from "./ITileSet";

export interface IMapLayer {
    data: number[];
}

export interface IMap {
    height: number;
    width: number;
    tileheight: number;
    tilewidth: number;
    tilesets: ITileSet[];
    layers: IMapLayer[];
}
import { IPlayer } from "./IPlayer.js";
import { IUnit } from "./IUnit.js";

interface PlayerBattleStats {
    id: string;
    name: string;
    totalHealth: number;
    unitCount: number;
}

export class ScoreBoard {
    private static readonly playerHeight = 30;

    private playerMap: Map<string, HTMLDivElement> = new Map();
    private battleStatsMap: Map<string, PlayerBattleStats> = new Map();
    private battleContainer: HTMLElement | null = null;
    private isGameActive: boolean = false;

    constructor(private container: HTMLElement) { }

    public get numberOfPlayers(): number {
        return this.playerMap.size;
    }

    public createOrUpdatePlayer(player: IPlayer) {
        let playerElem = this.playerMap.get(player.id);
        if (playerElem == null) {
            playerElem = this.createPlayerElem(player);
        } else {
            (playerElem.querySelector('.player-name') as HTMLDivElement).innerText = player.name;
            const scoreElem = playerElem.querySelector('.player-score');
            if (scoreElem) {
                scoreElem.innerHTML = player.score.toFixed(0);
            }
        }
    }

    public startBattle(playerIds: string[]) {
        this.isGameActive = true;
        this.battleStatsMap.clear();
        
        // Initialize battle stats for each player
        playerIds.forEach(id => {
            const playerElem = this.playerMap.get(id);
            if (playerElem) {
                const name = playerElem.querySelector('.player-name');
                if (name) {
                    this.battleStatsMap.set(id, {
                        id,
                        name: name.textContent || 'Unknown',
                        totalHealth: 0,
                        unitCount: 0
                    });
                }
            }
        });
        
        this.createBattleStatsDisplay();
    }

    public recordUnitStats(playerId: string, units: any[]) {
        if (!this.isGameActive) return;

        const stats = this.battleStatsMap.get(playerId);
        if (stats) {
            // Calculate total health and count of alive units
            stats.totalHealth = units.reduce((sum: number, u: any) => sum + (u.health > 0 ? u.health : 0), 0);
            stats.unitCount = units.filter((u: any) => u.health > 0).length;
            this.updateBattleDisplay(playerId);
        }
    }

    public endBattle() {
        this.isGameActive = false;
        if (this.battleContainer) {
            this.battleContainer.style.display = 'none';
        }
    }

    private createBattleStatsDisplay() {
        // Check if battle container already exists
        let container = document.querySelector('.battle-stats') as HTMLElement;
        if (!container) {
            container = document.createElement('div');
            container.classList.add('battle-stats');
            
            // Insert after the players container
            const playersContainer = this.container.parentElement;
            if (playersContainer) {
                playersContainer.insertBefore(container, this.container.nextSibling);
            }
        }
        
        this.battleContainer = container;
        this.battleContainer.style.display = 'unset';
        
        // Create header
        this.battleContainer.innerHTML = '<div class="battle-title">Active Battle</div>';
        
        // Add stats for each player in battle
        this.battleStatsMap.forEach(stats => {
            const playerBattleElem = document.createElement('div');
            playerBattleElem.classList.add('battle-player');
            playerBattleElem.id = `battle-${stats.id}`;
            playerBattleElem.innerHTML = `
                <div class="battle-player-name">${stats.name}</div>
                <div class="battle-player-health">
                    <span class="health-label">Health: </span>
                    <span class="health-value">${stats.totalHealth}</span>
                </div>
                <div class="battle-player-units">
                    <span class="units-label">Units: </span>
                    <span class="units-value">${stats.unitCount}</span>
                </div>
            `;
            this.battleContainer?.appendChild(playerBattleElem);
        });
    }

    private updateBattleDisplay(playerId: string) {
        if (!this.battleContainer) return;

        const stats = this.battleStatsMap.get(playerId);
        if (!stats) return;

        const playerElem = this.battleContainer.querySelector(`#battle-${playerId}`) as HTMLElement;
        if (playerElem) {
            const healthValueElem = playerElem.querySelector('.health-value');
            const unitsValueElem = playerElem.querySelector('.units-value');
            
            if (healthValueElem) {
                healthValueElem.textContent = stats.totalHealth.toString();
            }
            if (unitsValueElem) {
                unitsValueElem.textContent = stats.unitCount.toString();
            }
        }
    }

    private updatePlayerScore(player: IPlayer, playerElem: HTMLElement) {
        let players = Array.from(this.container.querySelectorAll('.player') as NodeListOf<HTMLElement>)
            .map((pe) => {
                const scoreElem = pe.querySelector('.player-score');
                const matchResult = /translateY\((\d+)/.exec(pe.style.transform);
                return {
                    score: scoreElem ? Number.parseInt(scoreElem.innerHTML) : 0,
                    position: matchResult ? Number.parseInt(matchResult[1]) : 0,
                    elem: pe
                };
            });
        players = players.sort((a, b) => {
            if (a.score === b.score) {
                return a.position - b.position;
            }
            return b.score - a.score;
        });
        const playerIndex = players.findIndex((pe) => pe.elem === playerElem);
        let newIndex = playerIndex;
        while (newIndex > 0 && players[newIndex - 1].score < player.score) {
            newIndex--;
        }
        if (newIndex < playerIndex) {
            playerElem.classList.add('hidden');
            setTimeout(() => {
                for (let i = newIndex; i < playerIndex; i++) {
                    players[i].elem.style.transform = `translateY(${(i + 1) * ScoreBoard.playerHeight}px)`;
                }
                playerElem.style.transform = `translateY(${newIndex * ScoreBoard.playerHeight}px)`;
                const scoreElem = playerElem.querySelector('.player-score');
                if (scoreElem) {
                    scoreElem.innerHTML = player.score.toFixed(0);
                }
                setTimeout(() => {
                    playerElem.classList.remove('hidden');
                }, 250)
            }, 250);
        } else {
            const scoreElem = playerElem.querySelector('.player-score');
            if (scoreElem) {
                scoreElem.innerHTML = player.score.toFixed(0);
            }
        }
    }

    private createPlayerElem(player: IPlayer) {
        const playerElem = this.getDivWithClass('player');
        const name = this.getDivWithClass('player-name');
        name.innerText = player.name;
        playerElem.appendChild(name);
        const score = this.getDivWithClass('player-score');
        score.innerText = player.score.toFixed(0);
        playerElem.appendChild(score);
        playerElem.style.transform = `translateY(${this.numberOfPlayers * ScoreBoard.playerHeight}px)`;
        this.playerMap.set(player.id, playerElem);
        this.container.appendChild(playerElem);
        return playerElem;
    }

    private getDivWithClass(className: string): HTMLDivElement {
        const elem = document.createElement('div');
        elem.classList.add(className);
        return elem;
    }
}
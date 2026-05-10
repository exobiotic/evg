import { Game } from './js/Game.js';
import { API } from './js/API.js';
import { ScoreBoard } from './js/ScoreBoard.js';
import { IPlayer } from './js/IPlayer.js';

const observerMode = (document.body.dataset.mode || '').toLowerCase() === 'observer';

let currentGame: Game | null = null;
let continuePlaying: boolean = !observerMode;  // Controllers auto-play, observers don't
let tournamentComplete: boolean = false;
const api = new API();
const scoreBoard = new ScoreBoard(document.querySelector('.players') as HTMLElement)
const startButton = document.querySelector('#start-button') as HTMLButtonElement;
const stopButton = document.querySelector('#stop-button') as HTMLButtonElement;
const game = (document.querySelector('#game') as HTMLDivElement);

function showTournamentSplash(winner?: IPlayer) {
    endGaming();
    const preInfo = document.querySelector('.pre-game') as HTMLElement;
    if (winner != null) {
        preInfo.innerHTML = `Tournament Winner<br/>${winner.name}`;
    } else {
        preInfo.innerHTML = 'Tournament Over<br/>Draw';
    }
    preInfo.style.display = 'unset';
}

function showGame() {
    startButton.style.display = 'none';
    if (currentGame != null) {
        currentGame.destroy();
    }
    stopButton.style.display = observerMode ? 'none' : 'unset';
    game.style.display = 'unset';

    currentGame = new Game(api, scoreBoard);
}

function endGaming() {
    if (currentGame != null) {
        currentGame.destroy();
    }
    currentGame = null;
    startButton.style.display = observerMode ? 'none' : 'unset';
    stopButton.style.display = 'none';
    game.style.display = 'none';
    scoreBoard.endBattle();

    if (observerMode) {
        const preInfo = document.querySelector('.pre-game') as HTMLElement;
        preInfo.innerHTML = 'Observer Mode<br />Watching<br />live game...';
        preInfo.style.display = 'unset';
    }
}

if (observerMode) {
    const preInfo = document.querySelector('.pre-game') as HTMLElement;
    preInfo.innerHTML = 'Observer Mode<br />Watching<br />live game...';
    startButton.style.display = 'none';
    stopButton.style.display = 'none';
}

const eventSource = new EventSource('/api/game');
eventSource.onmessage = (event) => {
    if (event == null || event.data == null) {
        return;
    }

    const action: { type: string, player?: IPlayer, winner?: IPlayer, players?: IPlayer[] } = JSON.parse(event.data);
    console.log(action.type);

    if (action.type === 'game-created') {
        tournamentComplete = false;
        console.log('Game has been created');
        // Extract player IDs and start battle stats tracking
        if (action.players && action.players.length > 0) {
            scoreBoard.startBattle(action.players.map(p => p.id));
        }
        showGame();
    } else if (action.type === 'game-ended') {
        console.log('Game ended');
        setTimeout(() => {
            if (!observerMode && continuePlaying && !tournamentComplete) {
                api.createGame();
            } else {
                endGaming();
            }
        }, 3000);
    } else if (action.type === 'tournament-complete') {
        tournamentComplete = true;
        continuePlaying = false;
        showTournamentSplash(action.winner);
    } else if (action.type.startsWith('player') && action.player != null) {
        console.log('Player updated');
        scoreBoard.createOrUpdatePlayer(action.player);
        if (scoreBoard.numberOfPlayers >= 2) {
            const preInfo = document.querySelector('.pre-game') as HTMLElement;
            if (preInfo.style.display == '') {
                preInfo.style.display = 'none'
                if (!observerMode) {
                    startButton.style.display = 'unset';
                }
            }
        }
    }
};

api.getGameSpec()
    .then((spec) => {
        if (spec != null && spec.active) {
            startButton.style.display = 'none';
            showGame();
        }
    })
    .catch(() => { });

api.getPlayers()
    .then((players) => {
        players = (players || []).sort((a: IPlayer, b: IPlayer) => b.score - a.score);
        players.forEach((p) => scoreBoard.createOrUpdatePlayer(p));
        if (scoreBoard.numberOfPlayers >= 2) {
            const preInfo = document.querySelector('.pre-game') as HTMLElement;
            if (preInfo.style.display == '') {
                preInfo.style.display = 'none'
                if (!observerMode) {
                    startButton.style.display = 'unset';
                }
            }
        }
    });

if (!observerMode) {
    startButton.addEventListener('click', () => {
        continuePlaying = true;
        tournamentComplete = false;
        const preInfo = document.querySelector('.pre-game') as HTMLElement;
        preInfo.innerHTML = 'Waiting<br />for<br />players...';
        preInfo.style.display = 'none';
        api.createGame();
        stopButton.disabled = false;
    });

    stopButton.addEventListener('click', () => {
        continuePlaying = false;
        stopButton.disabled = true;
    });
}

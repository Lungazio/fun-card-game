#!/bin/bash

# Deadman Ability Test Script
BASE_URL="http://localhost:5001/api/game"
API_KEY="poker-game-api-key-2024"

echo "💀 Testing Deadman Ability"
echo "=========================="

# 1. Create 4-player game
echo "1. Creating 4-player game..."
RESPONSE=$(curl -s -X POST "$BASE_URL/create" \
  -H "X-API-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "players": [
      {"id": 1, "name": "Alice", "startingFunds": 1000},
      {"id": 2, "name": "Bob", "startingFunds": 1000},
      {"id": 3, "name": "Charlie", "startingFunds": 1000},
      {"id": 4, "name": "Diana", "startingFunds": 1000}
    ],
    "smallBlind": 5,
    "bigBlind": 10
  }')

GAME_ID=$(echo $RESPONSE | jq -r '.GameId')
echo "Game ID: $GAME_ID"

# 2. Start game
echo "2. Starting game..."
curl -s -X POST "$BASE_URL/$GAME_ID/start" -H "X-API-Key: $API_KEY" > /dev/null

# 3. Identify deadman player
echo "3. Identifying Deadman player..."
GAME_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" -H "X-API-Key: $API_KEY")
DEADMAN_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.Players[] | select(.Abilities[]?.Type == "Deadman") | .Id')
DEADMAN_NAME=$(echo $GAME_STATE | jq -r ".GameState.Players[] | select(.Id == $DEADMAN_PLAYER) | .Name")
echo "Deadman player: $DEADMAN_NAME (ID: $DEADMAN_PLAYER)"

# 4. Make 2 non-deadman players fold
echo "4. Making 2 non-Deadman players fold..."
folded_count=0
attempts=0
max_attempts=20

while [ $folded_count -lt 2 ] && [ $attempts -lt $max_attempts ]; do
    GAME_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" -H "X-API-Key: $API_KEY")
    CURRENT_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.CurrentPlayerId')
    
    if [ "$CURRENT_PLAYER" = "null" ]; then
        echo "   No current player - betting round may be complete"
        break
    fi
    
    CURRENT_NAME=$(echo $GAME_STATE | jq -r ".GameState.Players[] | select(.Id == $CURRENT_PLAYER) | .Name")
    IS_FOLDED=$(echo $GAME_STATE | jq -r ".GameState.Players[] | select(.Id == $CURRENT_PLAYER) | .IsFolded")
    
    if [ "$IS_FOLDED" = "true" ]; then
        echo "   Current player $CURRENT_NAME is already folded - skipping"
        attempts=$((attempts + 1))
        continue
    fi
    
    echo "   Current turn: $CURRENT_NAME (ID: $CURRENT_PLAYER)"
    
    if [ "$CURRENT_PLAYER" != "$DEADMAN_PLAYER" ] && [ $folded_count -lt 2 ]; then
        echo "   Making $CURRENT_NAME fold..."
        curl -s -X POST "$BASE_URL/$GAME_ID/action" \
          -H "X-API-Key: $API_KEY" \
          -H "Content-Type: application/json" \
          -d "{\"playerId\": $CURRENT_PLAYER, \"actionType\": \"Fold\", \"amount\": 0}" > /dev/null
        folded_count=$((folded_count + 1))
        echo "   Folded players: $folded_count/2"
    else
        echo "   $CURRENT_NAME checks..."
        curl -s -X POST "$BASE_URL/$GAME_ID/action" \
          -H "X-API-Key: $API_KEY" \
          -H "Content-Type: application/json" \
          -d "{\"playerId\": $CURRENT_PLAYER, \"actionType\": \"Check\", \"amount\": 0}" > /dev/null
    fi
    
    attempts=$((attempts + 1))
done

echo "   Completed folding phase - $folded_count players folded"

# 5. Advance turns until Deadman player's turn
echo "5. Advancing to Deadman player's turn..."
attempts=0
max_attempts=15

while [ $attempts -lt $max_attempts ]; do
    GAME_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" -H "X-API-Key: $API_KEY")
    CURRENT_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.CurrentPlayerId')
    
    if [ "$CURRENT_PLAYER" = "$DEADMAN_PLAYER" ]; then
        echo "   It's $DEADMAN_NAME's turn!"
        break
    fi
    
    if [ "$CURRENT_PLAYER" = "null" ]; then
        echo "   No current player - checking game phase..."
        PHASE=$(echo $GAME_STATE | jq -r '.GameState.CurrentPhase')
        echo "   Game phase: $PHASE"
        break
    fi
    
    CURRENT_NAME=$(echo $GAME_STATE | jq -r ".GameState.Players[] | select(.Id == $CURRENT_PLAYER) | .Name")
    IS_FOLDED=$(echo $GAME_STATE | jq -r ".GameState.Players[] | select(.Id == $CURRENT_PLAYER) | .IsFolded")
    
    if [ "$IS_FOLDED" = "true" ]; then
        echo "   Current player $CURRENT_NAME is folded - this shouldn't happen"
        break
    fi
    
    echo "   Current turn: $CURRENT_NAME - checking..."
    curl -s -X POST "$BASE_URL/$GAME_ID/action" \
      -H "X-API-Key: $API_KEY" \
      -H "Content-Type: application/json" \
      -d "{\"playerId\": $CURRENT_PLAYER, \"actionType\": \"Check\", \"amount\": 0}" > /dev/null
    
    attempts=$((attempts + 1))
done

if [ $attempts -eq $max_attempts ]; then
    echo "   ⚠️ Could not reach Deadman player after $max_attempts attempts"
fi

# 6. Use Deadman ability
echo "6. Using Deadman ability..."
DEADMAN_RESPONSE=$(curl -s -X POST "$BASE_URL/$GAME_ID/abilities/use" \
  -H "X-API-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d "{\"playerId\": $DEADMAN_PLAYER, \"abilityType\": \"deadman\"}")

echo "Response:"
echo $DEADMAN_RESPONSE | jq '.'

# 7. Show results
echo ""
echo "7. Results:"
SUCCESS=$(echo $DEADMAN_RESPONSE | jq -r '.Success')
if [ "$SUCCESS" = "true" ]; then
    echo "✅ Deadman ability used successfully!"
    echo ""
    echo "Revealed folded players' cards:"
    echo $DEADMAN_RESPONSE | jq -r '.FoldedPlayers[]? | "   \(.PlayerName): \(.HoleCards | join(", "))"'
else
    echo "❌ Deadman ability failed:"
    echo $DEADMAN_RESPONSE | jq -r '.error // .Message'
fi

# 8. Cleanup
echo ""
echo "8. Cleaning up..."
curl -s -X DELETE "$BASE_URL/$GAME_ID" -H "X-API-Key: $API_KEY" > /dev/null
echo "✅ Test complete!"
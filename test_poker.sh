#!/bin/bash

# Fixed Poker API Test Suite
BASE_URL="http://localhost:5001/api/game"
API_KEY="poker-game-api-key-2024"

echo "🎯 Testing Fixed Poker API..."
echo "======================================"

# 1. Test Connection
echo "1. Testing API Connection..."
curl -X POST "$BASE_URL/test" \
  -H "X-API-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -w "\nStatus: %{http_code}\n\n"

# 2. Create a Game
echo "2. Creating a new game..."
RESPONSE=$(curl -s -X POST "$BASE_URL/create" \
  -H "X-API-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "players": [
      {"id": 1, "name": "Alice", "startingFunds": 1000},
      {"id": 2, "name": "Bob", "startingFunds": 1000}
    ],
    "smallBlind": 5,
    "bigBlind": 10
  }')

echo "Response: $RESPONSE"
GAME_ID=$(echo $RESPONSE | grep -o '"GameId":"[^"]*"' | cut -d'"' -f4)
echo "Game ID: $GAME_ID"
echo ""

if [ -z "$GAME_ID" ]; then
  echo "❌ Failed to create game. Exiting."
  exit 1
fi

# 3. Start the Game
echo "3. Starting the game..."
curl -X POST "$BASE_URL/$GAME_ID/start" \
  -H "X-API-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -w "\nStatus: %{http_code}\n\n"

# 4. Get Game State to see current player
echo "4. Getting game state to check current player..."
GAME_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" \
  -H "X-API-Key: $API_KEY")

echo "Game State: $GAME_STATE"
# Fixed: Use capital C for CurrentPlayerId and handle the nested JSON structure
CURRENT_PLAYER=$(echo $GAME_STATE | grep -o '"CurrentPlayerId":[^,}]*' | cut -d':' -f2 | tr -d ' ')
echo "Current Player ID: $CURRENT_PLAYER"
echo ""

# 5. Player Action - Use the actual current player
echo "5. Current player calls..."
curl -X POST "$BASE_URL/$GAME_ID/action" \
  -H "X-API-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "playerId": '$CURRENT_PLAYER',
    "actionType": "Call",
    "amount": 0
  }' \
  -w "\nStatus: %{http_code}\n\n"

# 6. Get updated state to see next player
echo "6. Getting updated game state..."
GAME_STATE2=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" \
  -H "X-API-Key: $API_KEY")

CURRENT_PLAYER2=$(echo $GAME_STATE2 | grep -o '"CurrentPlayerId":[^,}]*' | cut -d':' -f2 | tr -d ' ')
echo "Next Current Player ID: $CURRENT_PLAYER2"

# 7. Next player action
echo "7. Next player checks..."
curl -X POST "$BASE_URL/$GAME_ID/action" \
  -H "X-API-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "playerId": '$CURRENT_PLAYER2',
    "actionType": "Check",
    "amount": 0
  }' \
  -w "\nStatus: %{http_code}\n\n"

# 8. Test ability usage (after getting current player)
echo "8. Getting final state and testing ability..."
FINAL_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" \
  -H "X-API-Key: $API_KEY")

FINAL_CURRENT_PLAYER=$(echo $FINAL_STATE | grep -o '"CurrentPlayerId":[^,}]*' | cut -d':' -f2 | tr -d ' ')
echo "Final Current Player ID: $FINAL_CURRENT_PLAYER"

if [ "$FINAL_CURRENT_PLAYER" != "null" ] && [ ! -z "$FINAL_CURRENT_PLAYER" ]; then
  echo "Testing peek ability..."
  curl -X POST "$BASE_URL/$GAME_ID/abilities/use" \
    -H "X-API-Key: $API_KEY" \
    -H "Content-Type: application/json" \
    -d '{
      "playerId": '$FINAL_CURRENT_PLAYER',
      "abilityType": "peek",
      "targetPlayerId": '$(if [ "$FINAL_CURRENT_PLAYER" = "1" ]; then echo "2"; else echo "1"; fi)',
      "cardIndex": 0
    }' \
    -w "\nStatus: %{http_code}\n\n"
else
  echo "No current player to test ability with."
fi

# 9. Clean up
echo "9. Deleting the game..."
curl -X DELETE "$BASE_URL/$GAME_ID" \
  -H "X-API-Key: $API_KEY" \
  -w "\nStatus: %{http_code}\n\n"

echo "✅ Fixed API Test Suite Complete!"
echo "======================================"
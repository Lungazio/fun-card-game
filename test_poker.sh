#!/bin/bash

# Burn & Trashman Abilities Test Script
BASE_URL="http://localhost:5001/api/game"
API_KEY="poker-game-api-key-2024"

echo "🔥🗑️ Testing Burn & Trashman Abilities..."
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
curl -s -X POST "$BASE_URL/$GAME_ID/start" \
  -H "X-API-Key: $API_KEY" \
  -H "Content-Type: application/json" | jq '.'
echo ""

# 4. Get initial game state
echo "4. Getting initial game state..."
GAME_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" \
  -H "X-API-Key: $API_KEY")

echo "Initial abilities distribution:"
echo $GAME_STATE | jq '.GameState.Players[] | {id: .Id, name: .Name, abilities: .Abilities[].Type}'
echo ""

# Extract current player ID
CURRENT_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.CurrentPlayerId')
echo "Current Player ID: $CURRENT_PLAYER"

# 5. Check who has Burn ability
echo "5. Looking for Burn ability..."
BURN_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.Players[] | select(.Abilities[]?.Type == "Burn") | .Id')
echo "Player with Burn ability: $BURN_PLAYER"

# 6. Check who has Trashman ability  
echo "6. Looking for Trashman ability..."
TRASHMAN_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.Players[] | select(.Abilities[]?.Type == "Trashman") | .Id')
echo "Player with Trashman ability: $TRASHMAN_PLAYER"

# 7. Force game progression to create burnt cards
echo "7. Forcing game progression to create burnt cards..."

# Function to advance turns until we reach flop
advance_to_flop() {
  local max_attempts=10
  local attempts=0
  
  while [ $attempts -lt $max_attempts ]; do
    # Get current state
    GAME_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" \
      -H "X-API-Key: $API_KEY")
    
    CURRENT_PHASE=$(echo $GAME_STATE | jq -r '.GameState.CurrentPhase')
    CURRENT_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.CurrentPlayerId')
    
    echo "  Attempt $((attempts + 1)): Phase=$CURRENT_PHASE, Current Player=$CURRENT_PLAYER"
    
    # If we reached flop or beyond, we're done
    if [ "$CURRENT_PHASE" != "Preflop" ]; then
      echo "  ✅ Reached $CURRENT_PHASE phase!"
      return 0
    fi
    
    # If no current player, something's wrong
    if [ "$CURRENT_PLAYER" = "null" ]; then
      echo "  ❌ No current player - game might be stuck"
      return 1
    fi
    
    # Make the current player take a reasonable action
    echo "  Making player $CURRENT_PLAYER take action..."
    
    # Try call first, then check if that fails
    ACTION_RESPONSE=$(curl -s -X POST "$BASE_URL/$GAME_ID/action" \
      -H "X-API-Key: $API_KEY" \
      -H "Content-Type: application/json" \
      -d "{
        \"playerId\": $CURRENT_PLAYER,
        \"actionType\": \"Call\",
        \"amount\": 0
      }")
    
    ACTION_SUCCESS=$(echo $ACTION_RESPONSE | jq -r '.Success // false')
    
    if [ "$ACTION_SUCCESS" = "false" ]; then
      # Call failed, try check
      echo "  Call failed, trying check..."
      ACTION_RESPONSE=$(curl -s -X POST "$BASE_URL/$GAME_ID/action" \
        -H "X-API-Key: $API_KEY" \
        -H "Content-Type: application/json" \
        -d "{
          \"playerId\": $CURRENT_PLAYER,
          \"actionType\": \"Check\",
          \"amount\": 0
        }")
      
      ACTION_SUCCESS=$(echo $ACTION_RESPONSE | jq -r '.Success // false')
    fi
    
    if [ "$ACTION_SUCCESS" = "false" ]; then
      echo "  ❌ Both call and check failed for player $CURRENT_PLAYER"
      echo "  Response: $(echo $ACTION_RESPONSE | jq '.error // .message')"
      return 1
    fi
    
    echo "  ✅ Player $CURRENT_PLAYER took action successfully"
    attempts=$((attempts + 1))
    sleep 1
  done
  
  echo "  ❌ Failed to advance to flop after $max_attempts attempts"
  return 1
}

# Call the function to advance to flop
if advance_to_flop; then
  echo "7a. Successfully advanced past preflop!"
else
  echo "7a. Failed to advance past preflop, continuing with current state..."
fi

# 8. Get updated state after game progression
echo "8. Getting state after game progression..."
GAME_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" \
  -H "X-API-Key: $API_KEY")

CURRENT_PHASE=$(echo $GAME_STATE | jq -r '.GameState.CurrentPhase')
echo "Current phase: $CURRENT_PHASE"

CURRENT_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.CurrentPlayerId')
echo "Current player: $CURRENT_PLAYER"

# Re-identify ability holders after game progression
BURN_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.Players[] | select(.Abilities[]?.Type == "Burn") | .Id')
TRASHMAN_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.Players[] | select(.Abilities[]?.Type == "Trashman") | .Id')

echo "Updated - Burn player: $BURN_PLAYER, Trashman player: $TRASHMAN_PLAYER"

# 9. TEST BURN ABILITY (if player has it and it's their turn)
if [ "$BURN_PLAYER" != "null" ] && [ "$BURN_PLAYER" = "$CURRENT_PLAYER" ]; then
  echo "9. Testing Burn Ability..."
  
  echo "9a. Using Burn ability (reveal suit)..."
  BURN_RESPONSE=$(curl -s -X POST "$BASE_URL/$GAME_ID/abilities/use" \
    -H "X-API-Key: $API_KEY" \
    -H "Content-Type: application/json" \
    -d "{
      \"playerId\": $BURN_PLAYER,
      \"abilityType\": \"burn\",
      \"revealSuit\": true
    }")
  
  echo "Burn Response:"
  echo $BURN_RESPONSE | jq '.'
  echo ""
  
else
  echo "9. Skipping Burn test - either no burn ability or not their turn"
  echo "   Burn player: $BURN_PLAYER, Current player: $CURRENT_PLAYER"
  
  # Make current player take an action to continue
  if [ "$CURRENT_PLAYER" != "null" ]; then
    echo "9a. Current player checks to continue..."
    curl -s -X POST "$BASE_URL/$GAME_ID/action" \
      -H "X-API-Key: $API_KEY" \
      -H "Content-Type: application/json" \
      -d "{
        \"playerId\": $CURRENT_PLAYER,
        \"actionType\": \"Check\",
        \"amount\": 0
      }" | jq '.Success'
  fi
fi

# 10. Get current state and switch to Trashman player if needed
echo "10. Preparing for Trashman test..."
GAME_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" \
  -H "X-API-Key: $API_KEY")

CURRENT_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.CurrentPlayerId')
echo "Current player: $CURRENT_PLAYER"

# Try to get to the Trashman player's turn
if [ "$TRASHMAN_PLAYER" != "null" ] && [ "$TRASHMAN_PLAYER" != "$CURRENT_PLAYER" ]; then
  echo "10a. Switching to Trashman player's turn..."
  if [ "$CURRENT_PLAYER" != "null" ]; then
    curl -s -X POST "$BASE_URL/$GAME_ID/action" \
      -H "X-API-Key: $API_KEY" \
      -H "Content-Type: application/json" \
      -d "{
        \"playerId\": $CURRENT_PLAYER,
        \"actionType\": \"Check\",
        \"amount\": 0
      }" | jq '.Success'
    
    # Update current player
    GAME_STATE=$(curl -s -X GET "$BASE_URL/$GAME_ID/state" \
      -H "X-API-Key: $API_KEY")
    CURRENT_PLAYER=$(echo $GAME_STATE | jq -r '.GameState.CurrentPlayerId')
  fi
fi

# 11. TEST TRASHMAN ABILITY (only if player has it, it's their turn, AND there are burnt cards)
if [ "$TRASHMAN_PLAYER" != "null" ] && [ "$TRASHMAN_PLAYER" = "$CURRENT_PLAYER" ]; then
  echo "11. Testing Trashman Ability availability..."
  
  # Check current phase and burn pile status from game state
  CURRENT_PHASE=$(echo $GAME_STATE | jq -r '.GameState.CurrentPhase')
  echo "Current phase: $CURRENT_PHASE"
  
  # Estimate if there should be burnt cards based on phase
  if [ "$CURRENT_PHASE" = "Preflop" ]; then
    echo "11a. Preflop phase - checking if any burn abilities were used..."
    echo "Skipping Trashman test in preflop (no burnt cards yet)"
    echo "Trashman will be testable after flop is dealt"
  else
    echo "11a. Post-flop phase - burnt cards should be available"
    echo "Testing Trashman Ability (3-step process)..."
    
    echo "11b. Trashman Step 1 - Initial use..."
    TRASHMAN_STEP1=$(curl -s -X POST "$BASE_URL/$GAME_ID/abilities/use" \
      -H "X-API-Key: $API_KEY" \
      -H "Content-Type: application/json" \
      -d "{
        \"playerId\": $TRASHMAN_PLAYER,
        \"abilityType\": \"trashman\"
      }")
    
    echo "Trashman Step 1 Response:"
    echo $TRASHMAN_STEP1 | jq '.'
    echo ""
    
    # Check if we got burnt cards to choose from
    CHOICE_REQUIRED=$(echo $TRASHMAN_STEP1 | jq -r '.ChoiceRequired // false')
    TRASHMAN_SUCCESS=$(echo $TRASHMAN_STEP1 | jq -r '.Success // false')
    
    if [ "$TRASHMAN_SUCCESS" = "true" ] && [ "$CHOICE_REQUIRED" = "true" ]; then
      
      echo "11c. Trashman Step 2 - Choose burnt card (selecting index 0)..."
      TRASHMAN_STEP2=$(curl -s -X POST "$BASE_URL/$GAME_ID/abilities/use" \
        -H "X-API-Key: $API_KEY" \
        -H "Content-Type: application/json" \
        -d "{
          \"playerId\": $TRASHMAN_PLAYER,
          \"abilityType\": \"trashman\",
          \"burntCardIndex\": 0
        }")
      
      echo "Trashman Step 2 Response:"
      echo $TRASHMAN_STEP2 | jq '.'
      echo ""
      
      echo "11d. Trashman Step 3 - Choose hole card to discard (selecting index 0)..."
      TRASHMAN_STEP3=$(curl -s -X POST "$BASE_URL/$GAME_ID/abilities/use" \
        -H "X-API-Key: $API_KEY" \
        -H "Content-Type: application/json" \
        -d "{
          \"playerId\": $TRASHMAN_PLAYER,
          \"abilityType\": \"trashman\",
          \"burntCardIndex\": 0,
          \"holeCardIndex\": 0
        }")
      
      echo "Trashman Step 3 (Final) Response:"
      echo $TRASHMAN_STEP3 | jq '.'
      echo ""
      
      echo "Trashman Result Summary:"
      echo $TRASHMAN_STEP3 | jq '.Result'
      
    elif [ "$TRASHMAN_SUCCESS" = "false" ]; then
      echo "❌ Trashman failed (expected in preflop):"
      echo $TRASHMAN_STEP1 | jq '.Message'
      echo "This is normal - Trashman requires burnt cards from flop/turn/river or burn abilities"
    else
      echo "❌ Unexpected Trashman response:"
      echo $TRASHMAN_STEP1 | jq '.'
    fi
  fi
  
else
  echo "11. Skipping Trashman test:"
  echo "   Trashman player: $TRASHMAN_PLAYER"
  echo "   Current player: $CURRENT_PLAYER" 
  echo "   Reason: Either no trashman ability or not their turn"
fi

# 12. Final game state
echo ""
echo "12. Final game state..."
curl -s -X GET "$BASE_URL/$GAME_ID/state" \
  -H "X-API-Key: $API_KEY" | jq '.GameState | {phase: .CurrentPhase, players: .Players[] | {id: .Id, name: .Name, holeCards: .HoleCards, abilities: .Abilities[].Type}}'

# 13. Clean up
echo ""
echo "13. Cleaning up - deleting the game..."
curl -s -X DELETE "$BASE_URL/$GAME_ID" \
  -H "X-API-Key: $API_KEY" \
  -w "\nStatus: %{http_code}\n"

echo ""
echo "✅ Burn & Trashman Abilities Test Complete!"
echo "======================================"
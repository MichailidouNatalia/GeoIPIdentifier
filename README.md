# Create necessary directories
mkdir -p docker/sql docker/redis

# Create the Redis config file
echo 'Creating Redis configuration...'
cat > docker/redis/redis.conf << 'EOF'
# ... (paste the redis.conf content above)
EOF

# Create SQL init script
echo 'Creating SQL init script...'
cat > docker/sql/init.sql << 'EOF'
# ... (paste the init.sql content above)
EOF

# Build and start
docker-compose up --build
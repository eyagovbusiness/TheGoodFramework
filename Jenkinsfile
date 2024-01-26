pipeline {
    agent {
        label 'frontdockeragent'
    }
    environment {
        REGISTRY='registry.guildswarm.org'
    }
    stages{
        stage('Build Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                    if (env.CHANGE_ID != null) {
                          def version = readFile('version').trim()
                          env.VERSION = version
                          sh''' find . \\( -name "*.csproj" -o -name "*.sln" -o -name "NuGet.docker.config" \\) -print0 \
                           | tar -cvf projectfiles.tar --null -T -
                           '''
						  try {
							sh "docker build . -t registry.guildswarm.org/base-images/thegoodframework:${version} -t registry.guildswarm.org/base-images/thegoodframework:latest"
						  } finally {
							    sh "rm -f projectfiles.tar"
							  }
                           }
                        }
                    }
                }
            }
        stage('Push Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                        if (env.CHANGE_ID == null) { // this is for just the build once is passed
                                withCredentials([[$class: 'UsernamePasswordMultiBinding', credentialsId: 'harbor-base-images', usernameVariable: 'DOCKER_USERNAME', passwordVariable: 'DOCKER_PASSWORD']]) {
                                    sh "docker login -u \'${DOCKER_USERNAME}' -p $DOCKER_PASSWORD $REGISTRY"
                                    sh "docker push registry.guildswarm.org/base-images/thegoodframework:$version"
                                    sh "docker push registry.guildswarm.org/base-images/thegoodframework:latest"
                                    sh 'docker logout'
                                }
                            }
                        }
                    }
                }
            }
        stage('Remove Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                        if (env.CHANGE_ID == null) {
                            sh "docker rmi registry.guildswarm.org/base-images/thegoodframework:$version"
                            sh "docker rmi registry.guildswarm.org/base-images/thegoodframework:latest"
                            sh "docker rmi registry.guildswarm.org/base-images/alpine-base:latest"
                            }
                        }
                    }
                }
            }
        }  
    post {
        always{
            sh 'rm -rf *'
        }
        failure {
            echo "Pipeline failed. Do any necessary cleanup here."
        }
    }
}
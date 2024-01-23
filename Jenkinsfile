harbor-base-images #id harbor
pipeline {
    agent {
        label 'frontdockeragent'
    }
    environment {
        REGISTRY='registry.guildswarm.org'
        TOOL_LABEL = 'front'
    }
    stages{
        stage('Check stage'){
            steps {
                script{
                    container('dockertainer'){
                        echo "test here :)"
                    }
                }
            }
        }
        stage('Build and Push Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                            withCredentials([[$class: 'UsernamePasswordMultiBinding', credentialsId: 'harbor-base-images', usernameVariable: 'DOCKER_USERNAME', passwordVariable: 'DOCKER_PASSWORD']]) {
                                sh "docker login -u \'${DOCKER_USERNAME}' -p $DOCKER_PASSWORD $REGISTRY"
                                sh 'chmod +x deploy.sh'
                                sh './deploy.sh'
                                sh 'docker logout'
                            }
                        }
                    }
                }
            } 
        }
    post {
        failure {
            echo "Pipeline failed. Do any necessary cleanup here."
        }
    }
}